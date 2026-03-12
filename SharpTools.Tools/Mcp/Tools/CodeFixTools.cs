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
                throw new McpException($"No diagnostics with id '{diagnosticId}' were found in '{filePath}'.");
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

    private static Document GetDocumentByPath(ISolutionManager solutionManager, string filePath) {
        var documentId = solutionManager.CurrentSolution?.GetDocumentIdsWithFilePath(filePath).FirstOrDefault();
        if (documentId == null) {
            throw new McpException($"File is not part of the loaded solution: {filePath}");
        }

        var document = solutionManager.CurrentSolution?.GetDocument(documentId);
        return document ?? throw new McpException($"Could not load document from solution: {filePath}");
    }
}
