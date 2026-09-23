namespace eQuantic.UI.Code;

/// <summary>
/// A problem, where it is, and how bad — the squiggle under the code and the row in the problems
/// list are the same record. Diagnostics are DATA the app hands in, not something the editor
/// computes: the editor's job is to show them at the right place after every edit.
/// </summary>
public sealed record CodeDiagnostic(CodeRange Range, string Message,
    CodeDiagnosticSeverity Severity = CodeDiagnosticSeverity.Error)
{
    /// <summary>The rule that fired — "EQ2004", "CS0246" — shown beside the message.</summary>
    public string? Code { get; init; }

    /// <summary>Who said so: the compiler, the analyzer, the linter.</summary>
    public string? Source { get; init; }
}
