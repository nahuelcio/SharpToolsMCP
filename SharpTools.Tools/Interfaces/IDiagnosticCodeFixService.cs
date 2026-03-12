using Microsoft.CodeAnalysis.CodeFixes;

namespace SharpTools.Tools.Interfaces;

public interface IDiagnosticCodeFixService {
    Task<IReadOnlyList<DocumentDiagnosticInfo>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken);
    Task<IReadOnlyList<CodeFixOptionInfo>> GetCodeFixesAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken);
    Task<ApplyCodeFixResult> ApplyCodeFixAsync(Document document, Diagnostic diagnostic, string? fixTitle, int fixIndex, CancellationToken cancellationToken);
}

public sealed record DocumentDiagnosticInfo(
    Diagnostic Diagnostic,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn,
    IReadOnlyList<CodeFixOptionInfo> Fixes);

public sealed record CodeFixOptionInfo(
    string Title,
    string ProviderName,
    string? EquivalenceKey);

public sealed record ApplyCodeFixResult(
    string AppliedTitle,
    string ProviderName,
    int ChangedDocumentCount);
