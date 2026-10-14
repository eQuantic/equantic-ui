using eQuantic.UI.Compiler;
using eQuantic.UI.Compiler.CodeGen;

namespace eQuantic.UI.Conformance.Tests.Infrastructure;

/// <summary>
/// One reason the build refuses a case's C#: the id a build prints, its text, and the module it was
/// reported in, where the build reported it for one.
/// <para>
/// Read where a build reads it and by the build's own rule, never by a rule of the harness's. A
/// converter's diagnostic refuses when it is an <see cref="ConversionSeverity.Error"/>: that is the
/// line <c>ComponentCompiler.CollectDiagnostics</c> draws between a build error and a warning, which
/// the build prints and builds past. A compiled module refuses with each of its errors when it did not
/// succeed, as <c>AppBuild</c> prints them, and an error with no id is the <c>EQ0000</c> it prints for
/// one.
/// </para>
/// </summary>
/// <param name="Id">The diagnostic's id, <c>EQ2008</c> say.</param>
/// <param name="Text">What the build says.</param>
/// <param name="Module">The module the build compiled when it said it, or null for the case's own
/// code.</param>
public sealed record Refusal(string Id, string Text, string? Module = null)
{
    /// <summary>The refusals among what a converter reported: its errors, and none of its warnings.</summary>
    public static IReadOnlyList<Refusal> Of(IEnumerable<ConversionDiagnostic> diagnostics) =>
        diagnostics.Where(diagnostic => diagnostic.Severity == ConversionSeverity.Error)
            .Select(diagnostic => new Refusal(diagnostic.Code, diagnostic.Message))
            .ToList();

    /// <summary>
    /// The refusals of compiled modules: every error of every module that did not succeed, whether it
    /// wrote text or not. A module the build stops at before its emitter runs (two methods of one name,
    /// EQ1007) writes none, so a reader that kept only the modules with text before asking dropped the
    /// one refusal it had.
    /// </summary>
    public static IReadOnlyList<Refusal> Of(IEnumerable<CompilationResult> modules) =>
        modules.Where(module => !module.Success)
            .SelectMany(module => module.Errors.Count == 0
                ? [new Refusal("EQ0000", "the module failed with no diagnostic", module.ComponentName)]
                : module.Errors.Select(error => new Refusal(
                    string.IsNullOrEmpty(error.Code) ? "EQ0000" : error.Code, error.Message, module.ComponentName)))
            .ToList();

    public override string ToString() => Module is null ? $"{Id}: {Text}" : $"{Id} in {Module}: {Text}";
}
