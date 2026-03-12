using System.Collections.Immutable;
using System.Composition.Hosting;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using ModelContextProtocol;

namespace SharpTools.Tools.Services;

public sealed class DiagnosticCodeFixService : IDiagnosticCodeFixService {
    private readonly ISolutionManager _solutionManager;
    private readonly ILogger<DiagnosticCodeFixService> _logger;
    private readonly Lazy<ImmutableArray<CodeFixProvider>> _codeFixProviders;

    public DiagnosticCodeFixService(ISolutionManager solutionManager, ILogger<DiagnosticCodeFixService> logger) {
        _solutionManager = solutionManager ?? throw new ArgumentNullException(nameof(solutionManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _codeFixProviders = new Lazy<ImmutableArray<CodeFixProvider>>(LoadCodeFixProviders);
    }

    public async Task<IReadOnlyList<DocumentDiagnosticInfo>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(document);

        var diagnostics = await GetDiagnosticsForDocumentCoreAsync(document, cancellationToken);
        var results = new List<DocumentDiagnosticInfo>(diagnostics.Count);

        foreach (var diagnostic in diagnostics) {
            cancellationToken.ThrowIfCancellationRequested();
            var span = diagnostic.Location.GetLineSpan();
            var fixes = await GetCodeFixesAsync(document, diagnostic, cancellationToken);
            results.Add(new DocumentDiagnosticInfo(
                diagnostic,
                span.StartLinePosition.Line + 1,
                span.StartLinePosition.Character + 1,
                span.EndLinePosition.Line + 1,
                span.EndLinePosition.Character + 1,
                fixes));
        }

        return results
            .OrderBy(d => d.StartLine)
            .ThenBy(d => d.StartColumn)
            .ThenBy(d => d.Diagnostic.Id)
            .ToList();
    }

    public async Task<IReadOnlyList<CodeFixOptionInfo>> GetCodeFixesAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(diagnostic);

        var matchingProviders = _codeFixProviders.Value
            .Where(provider =>
                provider.FixableDiagnosticIds.Contains(diagnostic.Id, StringComparer.OrdinalIgnoreCase))
            .ToList();

        var fixes = new List<CodeFixOptionInfo>();
        foreach (var provider in matchingProviders) {
            cancellationToken.ThrowIfCancellationRequested();
            var actions = new List<CodeAction>();
            var context = new CodeFixContext(
                document,
                diagnostic,
                (action, _) => actions.Add(action),
                cancellationToken);

            try {
                await provider.RegisterCodeFixesAsync(context);
            } catch (Exception ex) when (ex is not OperationCanceledException) {
                _logger.LogWarning(ex, "Code fix provider {Provider} failed for diagnostic {DiagnosticId}", provider.GetType().FullName, diagnostic.Id);
                continue;
            }

            fixes.AddRange(actions.Select(action => new CodeFixOptionInfo(
                action.Title,
                provider.GetType().FullName ?? provider.GetType().Name,
                action.EquivalenceKey)));
        }

        return fixes
            .GroupBy(f => new { f.Title, f.ProviderName, f.EquivalenceKey })
            .Select(g => g.First())
            .OrderBy(f => f.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<ApplyCodeFixResult> ApplyCodeFixAsync(Document document, Diagnostic diagnostic, string? fixTitle, int fixIndex, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(diagnostic);

        if (_solutionManager.CurrentWorkspace is not MSBuildWorkspace workspace) {
            throw new McpException("Current workspace is not available for applying code fixes.");
        }

        if (fixIndex < 1) {
            throw new McpException("fixIndex must be greater than or equal to 1.");
        }

        var candidateFixes = new List<(CodeAction action, string providerName)>();
        var matchingProviders = _codeFixProviders.Value
            .Where(provider =>
                provider.FixableDiagnosticIds.Contains(diagnostic.Id, StringComparer.OrdinalIgnoreCase))
            .ToList();

        foreach (var provider in matchingProviders) {
            cancellationToken.ThrowIfCancellationRequested();
            var actions = new List<CodeAction>();
            var context = new CodeFixContext(
                document,
                diagnostic,
                (action, _) => actions.Add(action),
                cancellationToken);

            await provider.RegisterCodeFixesAsync(context);
            candidateFixes.AddRange(actions.Select(action => (
                action,
                provider.GetType().FullName ?? provider.GetType().Name)));
        }

        if (!string.IsNullOrWhiteSpace(fixTitle)) {
            candidateFixes = candidateFixes
                .Where(f => string.Equals(f.action.Title, fixTitle, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (candidateFixes.Count == 0) {
            throw new McpException($"No code fixes were found for diagnostic '{diagnostic.Id}'.");
        }

        var orderedFixes = candidateFixes
            .OrderBy(f => f.action.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(f => f.providerName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (fixIndex > orderedFixes.Count) {
            throw new McpException($"fixIndex {fixIndex} is out of range. Matching fixes: {orderedFixes.Count}.");
        }

        var selectedFix = orderedFixes[fixIndex - 1];
        var operations = await selectedFix.action.GetOperationsAsync(cancellationToken);
        var applyChangesOperation = operations.OfType<ApplyChangesOperation>().FirstOrDefault();
        if (applyChangesOperation == null) {
            throw new McpException($"The selected code fix '{selectedFix.action.Title}' did not produce an ApplyChangesOperation.");
        }

        var newSolution = applyChangesOperation.ChangedSolution;
        var changedDocumentCount = newSolution.GetChanges(document.Project.Solution)
            .GetProjectChanges()
            .Sum(pc => pc.GetChangedDocuments().Count() + pc.GetAddedDocuments().Count() + pc.GetRemovedDocuments().Count());

        if (!workspace.TryApplyChanges(newSolution)) {
            throw new McpException($"Failed to apply code fix '{selectedFix.action.Title}' to the workspace.");
        }

        _solutionManager.RefreshCurrentSolution();

        return new ApplyCodeFixResult(selectedFix.action.Title, selectedFix.providerName, changedDocumentCount);
    }

    private async Task<IReadOnlyList<Diagnostic>> GetDiagnosticsForDocumentCoreAsync(Document document, CancellationToken cancellationToken) {
        var project = document.Project;
        var compilation = await project.GetCompilationAsync(cancellationToken);
        if (compilation == null) {
            throw new McpException($"Could not create a compilation for project '{project.Name}'.");
        }

        var documentTree = await document.GetSyntaxTreeAsync(cancellationToken);
        if (documentTree == null) {
            throw new McpException($"Could not get a syntax tree for '{document.FilePath}'.");
        }

        var diagnostics = new List<Diagnostic>();
        diagnostics.AddRange(compilation.GetDiagnostics(cancellationToken)
            .Where(d => IsInDocument(d, documentTree)));

        var analyzers = project.AnalyzerReferences
            .SelectMany(reference => reference.GetAnalyzers(project.Language))
            .Distinct(AnalyzerReferenceIdentityComparer.Instance)
            .ToImmutableArray();

        if (!analyzers.IsDefaultOrEmpty) {
            var compilationWithAnalyzers = compilation.WithAnalyzers(
                analyzers,
                new CompilationWithAnalyzersOptions(
                    project.AnalyzerOptions,
                    onAnalyzerException: null,
                    concurrentAnalysis: true,
                    logAnalyzerExecutionTime: false,
                    reportSuppressedDiagnostics: false));

            diagnostics.AddRange((await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken))
                .Where(d => IsInDocument(d, documentTree)));
        }

        return diagnostics
            .Where(d => d.Location != Location.None && d.Location.IsInSource)
            .GroupBy(d => new {
                d.Id,
                d.Severity,
                SpanStart = d.Location.SourceSpan.Start,
                SpanLength = d.Location.SourceSpan.Length,
                Message = d.GetMessage()
            })
            .Select(g => g.First())
            .ToList();
    }

    private static bool IsInDocument(Diagnostic diagnostic, SyntaxTree documentTree) {
        return diagnostic.Location != Location.None &&
               diagnostic.Location.IsInSource &&
               diagnostic.Location.SourceTree == documentTree;
    }

    private static ImmutableArray<CodeFixProvider> LoadCodeFixProviders() {
        var assemblies = MefHostServices.DefaultAssemblies.ToList();
        AddAssemblyIfFound(assemblies, "Microsoft.CodeAnalysis.Features");
        AddAssemblyIfFound(assemblies, "Microsoft.CodeAnalysis.CSharp.Features");

        var configuration = new ContainerConfiguration().WithAssemblies(assemblies.Distinct());
        using var container = configuration.CreateContainer();
        return container.GetExports<CodeFixProvider>().ToImmutableArray();
    }

    private static void AddAssemblyIfFound(ICollection<Assembly> assemblies, string assemblyName) {
        try {
            assemblies.Add(Assembly.Load(assemblyName));
        } catch {
            // Ignore missing optional feature assembly and let provider lookup proceed with what is available.
        }
    }

    private sealed class AnalyzerReferenceIdentityComparer : IEqualityComparer<DiagnosticAnalyzer> {
        public static readonly AnalyzerReferenceIdentityComparer Instance = new();

        public bool Equals(DiagnosticAnalyzer? x, DiagnosticAnalyzer? y) {
            if (ReferenceEquals(x, y)) {
                return true;
            }

            if (x is null || y is null) {
                return false;
            }

            return x.GetType() == y.GetType();
        }

        public int GetHashCode(DiagnosticAnalyzer obj) {
            return obj.GetType().GetHashCode();
        }
    }
}
