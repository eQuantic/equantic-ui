namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>Severity of a transpilation diagnostic.</summary>
public enum ConversionSeverity
{
    /// <summary>The construct was emitted but may misbehave; the build still succeeds.</summary>
    Warning,

    /// <summary>The construct cannot be transpiled; the build fails.</summary>
    Error,
}

/// <summary>
/// A diagnostic raised while transpiling C# to TypeScript — either a construct with no transpilation
/// strategy (emitted verbatim, <see cref="ConversionSeverity.Warning"/>) or a genuinely impossible
/// construct with no JS equivalent (<see cref="ConversionSeverity.Error"/>). Replaces the previous
/// behaviour of silently emitting unconvertible C# verbatim.
/// </summary>
/// <param name="Severity">Whether this fails the build or is advisory.</param>
/// <param name="Code">Stable diagnostic id (e.g. <c>EQ2001</c>) for suppression/lookup.</param>
/// <param name="Message">Human-readable explanation.</param>
/// <param name="Line">1-based source line.</param>
/// <param name="Column">1-based source column.</param>
public sealed record ConversionDiagnostic(
    ConversionSeverity Severity,
    string Code,
    string Message,
    int Line,
    int Column);
