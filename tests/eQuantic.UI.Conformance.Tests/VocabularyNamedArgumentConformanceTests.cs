using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A vocabulary record built with a named argument and an object initializer, read on both sides, in
/// TypeScript and in plain JavaScript. The twin takes its positional parameters and then a config object
/// with what the initializer set. The emitter placed the named argument in its parameter, then counted the
/// arguments the SYNTAX wrote to fill the defaults before the config: `new TextRun(code, Mono: true) { … }`
/// wrote `(code, null, true, false, { … })`, one default too many, and the initializer landed in a slot the
/// twin never reads (#1756).
/// </summary>
public class VocabularyNamedArgumentConformanceTests
{
    private const string Source = """
        using eQuantic.UI.Primitives;
        public static class Runs
        {
            public static string Of(TextRun run) =>
                run.Content + "|" + (run.Color == null ? "no color" : "color") + "|" + (run.Mono ? "mono" : "plain")
                + "|" + (run.Italic ? "italic" : "upright") + "|" + (run.StyleOverride is { } style ? style.Size : 0);
        }
        """;

    private static readonly (string Name, string Statements)[] Cases =
    [
        ("a named argument beside an initializer",
            "return Runs.Of(new TextRun(\"code\", Mono: true) { StyleOverride = new TypeStyle(13, 21, FontWeight.Regular, 0, 1.3f, true) });"),
        ("every argument named, out of order, beside an initializer",
            "return Runs.Of(new TextRun(Mono: true, Content: \"x\") { Italic = true });"),
        ("a named argument alone", "return Runs.Of(new TextRun(\"code\", Mono: true));"),
        ("every argument positional beside an initializer", "return Runs.Of(new TextRun(\"code\", null, true) { Italic = true });"),
        ("a named argument skipping one, beside an initializer",
            "return Runs.Of(new TextRun(Content: \"y\", Mono: true) { Italic = true, StyleOverride = new TypeStyle(12, 18, FontWeight.Bold, 0, 1.2f) });"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AVocabularyRecordsNamedArgumentAndInitializer_BothReachTheTwin(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Source, typeAnnotations, Cases);
}
