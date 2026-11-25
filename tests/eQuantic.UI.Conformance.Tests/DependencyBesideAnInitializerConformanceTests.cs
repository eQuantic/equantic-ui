using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A component whose constructor takes a dependency, built with an object initializer, read on both sides, in
/// TypeScript and in plain JavaScript (#1827). The twin's constructor leaves the dependency out of its signature and
/// resolves it in its body, so the call drops the dependency's argument. The defaults it then filled before the
/// config were counted over the C# parameters, the dependency included: one default too many went before the
/// config, which landed past the twin's last parameter, and with no argument after the dependency the first data
/// parameter took the dependency's default, <c>null</c>. A nullable dependency is handed over as it comes, so the
/// browser needs no clock registered to build these.
/// </summary>
public class DependencyBesideAnInitializerConformanceTests
{
    private const string Source = """
        using eQuantic.UI.Primitives;
        public sealed class Quark : StatelessComponent
        {
            private readonly IClock? _clock;
            public string Mood { get; }
            public int Size { get; init; }
            public Quark(IClock? clock, string mood = "calm") { _clock = clock; Mood = mood; }
            public string Says() => Mood + "|" + Size;
            public override VisualNode Build(ComponentContext context) => new Text(Says(), TypeRole.BodyM);
        }
        public sealed class Middle : StatelessComponent
        {
            private readonly IClock? _clock;
            public string Before { get; }
            public int After { get; }
            public string Tone { get; init; } = "plain";
            public Middle(string before, IClock? clock, int after = 3) { Before = before; _clock = clock; After = after; }
            public string Says() => Before + "|" + After + "|" + Tone;
            public override VisualNode Build(ComponentContext context) => new Text(Says(), TypeRole.BodyM);
        }
        """;

    private static readonly (string Name, string Statements)[] Cases =
    [
        ("a named argument after the dependency, beside an initializer", "return new Quark(null, mood: \"x\") { Size = 3 }.Says();"),
        ("a positional argument after the dependency, beside an initializer", "return new Quark(null, \"y\") { Size = 4 }.Says();"),
        ("no argument after the dependency, beside an initializer", "return new Quark(null) { Size = 5 }.Says();"),
        ("the dependency in the middle, the default after it left out, beside an initializer", "return new Middle(\"a\", null) { Tone = \"warm\" }.Says();"),
        ("the dependency in the middle, every argument named, beside an initializer", "return new Middle(after: 9, clock: null, before: \"b\") { Tone = \"cool\" }.Says();"),
        ("a target-typed construction after the dependency, beside an initializer", "Quark q = new(null, \"z\") { Size = 6 }; return q.Says();"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AComponentWithADependency_KeepsItsArgumentsAndItsInitializer(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Source, typeAnnotations, Cases);
}
