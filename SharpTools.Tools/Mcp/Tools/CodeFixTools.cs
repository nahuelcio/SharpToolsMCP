using ModelContextProtocol;
using SharpTools.Tools.Mcp;

namespace SharpTools.Tools.Mcp.Tools;

public class CodeFixToolsLogCategory { }

[McpServerToolType]
public static class CodeFixTools {
    [McpServerTool(Name = ToolHelpers.SharpToolPrefix + nameof(ListDocumentDiagnostics), Idempotent = true, ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Lists diagnostics for a source document and includes available quick actions/code fixes for each diagnostic.")]
    public static async Task<string> ListDocumentDiagnostics(
        ISolutionManager solutionManager,
        IDiagnosticCodeFixService diagnosticCodeFixService,
        ILogger<CodeFixToolsLogCategory> logger,
        [Description("The absolute path to the document to inspect.")] string filePath,
        [Description("Optional diagnostic id filter such as IDE0290. Leave empty to return all diagnostics.")] string? diagnosticId,
        [Description("Optional 1-based line filter. Use 0 to ignore.")] int lineNumber,
        CancellationToken cancellationToken = default) {

        return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(async () => {
            ErrorHandlingHelpers.ValidateStringParameter(filePath, nameof(filePath), logger);
            ToolHelpers.EnsureSolutionLoadedWithDetails(solutionManager, logger, nameof(ListDocumentDiagnostics));

            var document = GetDocumentByPath(solutionManager, filePath);
            var diagnostics = await diagnosticCodeFixService.GetDocumentDiagnosticsAsync(document, cancellationToken);

            if (!string.IsNullOrWhiteSpace(diagnosticId)) {
                diagnostics = diagnostics
                    .Where(d => string.Equals(d.Diagnostic.Id, diagnosticId, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (lineNumber > 0) {
                diagnostics = diagnostics
                    .Where(d => d.StartLine <= lineNumber && d.EndLine >= lineNumber)
                    .ToList();
            }

            return ToolHelpers.ToJson(new {
                filePath,
                diagnosticCount = diagnostics.Count,
                diagnostics = diagnostics.Select((item, index) => new {
                    occurrence = index + 1,
                    id = item.Diagnostic.Id,
                    severity = item.Diagnostic.Severity.ToString(),
                    message = item.Diagnostic.GetMessage(),
                    line = item.StartLine,
                    column = item.StartColumn,
                    endLine = item.EndLine,
                    endColumn = item.EndColumn,
                    fixes = item.Fixes.Select((fix, fixIndex) => new {
                        fixIndex = fixIndex + 1,
                        title = fix.Title,
                        providerName = fix.ProviderName,
                        equivalenceKey = fix.EquivalenceKey
                    })
                })
            });
        }, logger, nameof(ListDocumentDiagnostics), cancellationToken);
    }

    [McpServerTool(Name = ToolHelpers.SharpToolPrefix + nameof(ApplyCodeFix), Idempotent = false, ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Applies a Roslyn quick action/code fix to a document, such as IDE0290 'Use primary constructor'.")]
    public static async Task<string> ApplyCodeFix(
        ISolutionManager solutionManager,
        IDiagnosticCodeFixService diagnosticCodeFixService,
        ILogger<CodeFixToolsLogCategory> logger,
        [Description("The absolute path to the document to modify.")] string filePath,
        [Description("The diagnostic id to fix, such as IDE0290.")] string diagnosticId,
        [Description("1-based occurrence index after filtering by diagnostic id and optional line number.")] int occurrence,
        [Description("Optional 1-based line filter to disambiguate diagnostics. Use 0 to ignore.")] int lineNumber,
        [Description("Optional exact code fix title. Leave empty to use fixIndex.")] string? fixTitle,
        [Description("1-based fix index among the matching code fixes.")] int fixIndex,
        CancellationToken cancellationToken = default) {

        return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(async () => {
            ErrorHandlingHelpers.ValidateStringParameter(filePath, nameof(filePath), logger);
            ErrorHandlingHelpers.ValidateStringParameter(diagnosticId, nameof(diagnosticId), logger);
            ToolHelpers.EnsureSolutionLoadedWithDetails(solutionManager, logger, nameof(ApplyCodeFix));

            if (occurrence < 1) {
                throw new McpException("occurrence must be greater than or equal to 1.");
            }

            var document = GetDocumentByPath(solutionManager, filePath);
            var diagnostics = await diagnosticCodeFixService.GetDocumentDiagnosticsAsync(document, cancellationToken);
            diagnostics = diagnostics
                .Where(d => string.Equals(d.Diagnostic.Id, diagnosticId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (lineNumber > 0) {
                diagnostics = diagnostics
                    .Where(d => d.StartLine <= lineNumber && d.EndLine >= lineNumber)
                    .ToList();
            }

            if (diagnostics.Count == 0) {
                throw new McpException(
                    $"No diagnostics with id '{diagnosticId}' were found in '{filePath}'. " +
                    $"Try running '{ToolHelpers.SharpToolPrefix}{nameof(FormatCode)}' with diagnosticIds='{diagnosticId}'.");
            }

            if (occurrence > diagnostics.Count) {
                throw new McpException($"occurrence {occurrence} is out of range. Matching diagnostics: {diagnostics.Count}.");
            }

            var selectedDiagnostic = diagnostics[occurrence - 1];
            var result = await diagnosticCodeFixService.ApplyCodeFixAsync(document, selectedDiagnostic.Diagnostic, fixTitle, fixIndex, cancellationToken);

            return ToolHelpers.ToJson(new {
                filePath,
                diagnosticId,
                occurrence,
                lineNumber,
                appliedTitle = result.AppliedTitle,
                providerName = result.ProviderName,
                changedDocumentCount = result.ChangedDocumentCount
            });
        }, logger, nameof(ApplyCodeFix), cancellationToken);
    }
    [McpServerTool(Name = ToolHelpers.SharpToolPrefix + nameof(FormatCode), Idempotent = false, ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Runs `dotnet format` on a solution/project/document. Use this to apply bulk style/analyzer fixes such as IDE0063 when individual code fixes are not discoverable through Roslyn quick actions.")]
    public static async Task<string> FormatCode(
        ISolutionManager solutionManager,
        ILogger<CodeFixToolsLogCategory> logger,
        [Description("Absolute path to .sln, .csproj, or .cs. If empty, uses the currently loaded solution path.")] string? targetPath,
        [Description("Optional comma-separated diagnostic IDs (for example: IDE0063,IDE0059). If empty, runs full dotnet format.")] string? diagnosticIds,
        [Description("When targetPath is a .cs file, restrict formatting to that file with --include.")] bool includeSingleFileWhenTargetIsDocument = true,
        CancellationToken cancellationToken = default) {

        return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(async () => {
            ToolHelpers.EnsureSolutionLoadedWithDetails(solutionManager, logger, nameof(FormatCode));

            var resolvedTargetPath = ResolveFormatTargetPath(solutionManager, targetPath);
            var extension = Path.GetExtension(resolvedTargetPath);
            var isDocumentTarget = string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase);

            List<string>? includePaths = null;
            string? documentPath = null;
            if (isDocumentTarget) {
                var document = GetDocumentByPath(solutionManager, resolvedTargetPath);
                if (string.IsNullOrWhiteSpace(document.Project.FilePath)) {
                    throw new McpException($"Could not determine project file path for document '{resolvedTargetPath}'.");
                }

                documentPath = Path.GetFullPath(resolvedTargetPath);
                resolvedTargetPath = document.Project.FilePath;

                if (includeSingleFileWhenTargetIsDocument) {
                    includePaths = new List<string> { documentPath };
                }
            }

            string? beforeText = null;
            if (!string.IsNullOrWhiteSpace(documentPath) && File.Exists(documentPath)) {
                beforeText = await File.ReadAllTextAsync(documentPath, cancellationToken);
            }

            var formatResult = await DotnetFormatRunner.RunAsync(resolvedTargetPath, diagnosticIds, includePaths, cancellationToken);
            EnsureDotnetFormatSucceeded(formatResult, logger, nameof(FormatCode));

            bool changed = false;
            if (!string.IsNullOrWhiteSpace(documentPath) && beforeText != null && File.Exists(documentPath)) {
                var afterText = await File.ReadAllTextAsync(documentPath, cancellationToken);
                changed = !string.Equals(beforeText, afterText, StringComparison.Ordinal);
            }

            await solutionManager.ReloadSolutionFromDiskAsync(cancellationToken);

            return ToolHelpers.ToJson(new {
                requestedTargetPath = targetPath,
                resolvedTargetPath,
                isDocumentTarget,
                includeSingleFileWhenTargetIsDocument,
                diagnosticIds,
                changed,
                commandLine = formatResult.CommandLine,
                exitCode = formatResult.ExitCode,
                standardOutput = Truncate(formatResult.StandardOutput, 6000),
                standardError = Truncate(formatResult.StandardError, 6000)
            });
        }, logger, nameof(FormatCode), cancellationToken);
    }

    private static Document GetDocumentByPath(ISolutionManager solutionManager, string filePath) {
        var documentId = solutionManager.CurrentSolution?.GetDocumentIdsWithFilePath(filePath).FirstOrDefault();
        if (documentId == null) {
            throw new McpException($"File is not part of the loaded solution: {filePath}");
        }

        var document = solutionManager.CurrentSolution?.GetDocument(documentId);
        return document ?? throw new McpException($"Could not load document from solution: {filePath}");
    }
    private static void EnsureDotnetFormatSucceeded(DotnetFormatRunResult result, ILogger logger, string operationName) {
        if (result.ExitCode == 0) {
            return;
        }

        logger.LogError(
            "dotnet format failed during {Operation}. ExitCode={ExitCode}. Command={Command}. StdErr={StdErr}",
            operationName,
            result.ExitCode,
            result.CommandLine,
            result.StandardError);

        throw new McpException(
            $"dotnet format failed with exit code {result.ExitCode}. Command: {result.CommandLine}. " +
            $"Error: {Truncate(result.StandardError, 2000)}");
    }
    private static string ResolveFormatTargetPath(ISolutionManager solutionManager, string? targetPath) {
        if (!string.IsNullOrWhiteSpace(targetPath)) {
            var resolvedInputPath = Path.GetFullPath(targetPath);
            if (!File.Exists(resolvedInputPath)) {
                throw new McpException($"Target path does not exist: {resolvedInputPath}");
            }

            var extension = Path.GetExtension(resolvedInputPath);
            if (string.Equals(extension, ".sln", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".csproj", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase)) {
                return resolvedInputPath;
            }

            throw new McpException("targetPath must point to a .sln, .csproj, or .cs file.");
        }

        var loadedSolutionPath = solutionManager.CurrentSolution?.FilePath;
        if (string.IsNullOrWhiteSpace(loadedSolutionPath)) {
            throw new McpException("No targetPath provided and no loaded solution path is available.");
        }

        return loadedSolutionPath;
    }
    private static string Truncate(string? text, int maxLength) {
        if (string.IsNullOrEmpty(text)) {
            return string.Empty;
        }

        if (text.Length <= maxLength) {
            return text;
        }

        return $"{text[..maxLength]}...";
    }
}
