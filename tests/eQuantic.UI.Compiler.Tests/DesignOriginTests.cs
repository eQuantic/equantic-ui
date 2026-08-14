using System.Text.RegularExpressions;
using eQuantic.UI.Compiler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// Design mode: every node construction is emitted wrapped in <c>$eq.origin(…, "span")</c> so the
/// built node remembers the C# that made it.
/// <para>
/// This exists because the V3 source maps CANNOT answer the question. The whole <c>Build</c> body is
/// converted to one flat string and emitted through a single <c>Raw</c> call, so the finest position
/// a map can name is the start of the method — a measured 942-character line in one shipped module.
/// An origin is exact instead: the span of the expression that constructed the node.
/// </para>
/// </summary>
public class DesignOriginTests
{
    private const string Source = """
        using eQuantic.UI.Core;
        using eQuantic.UI.Primitives;

        [Page("/probe")]
        public sealed class Probe : StatefulComponent
        {
            private int _count;
            private readonly string[] _rows = { "a", "b", "c" };

            public override VisualNode Build(ComponentContext context)
            {
                var theme = context.Theme;
                var column = new Column(gap: Space.S2);
                foreach (var row in _rows)
                {
                    column.Add(new Text(row, TypeRole.BodyM, theme.TextPrimary));
                }
                column.Add(new Button("Up", onPressed: () => SetState(() => _count++)));
                column.Add(Footer(theme));
                return new Box(new BoxStyle { Padding = EdgeInsets.All(Space.S4) }, column);
            }

            private VisualNode Footer(IAppTheme theme) =>
                new Text("footer", TypeRole.Caption, theme.TextMuted);
        }
        """;

    /// <summary>
    /// A compilation with the framework's real assemblies, because stamping REQUIRES the semantic
    /// model: it has to know a node from a value, and without a model it deliberately stamps nothing
    /// rather than guess. Seeded by TYPE rather than by whatever the host happens to have loaded —
    /// the CLR loads lazily, so `GetAssemblies()` alone is complete only by luck.
    /// </summary>
    private static readonly MetadataReference[] References =
        new[]
            {
                typeof(eQuantic.UI.Primitives.VisualNode).Assembly,
                typeof(eQuantic.UI.Components.UI).Assembly,
                typeof(eQuantic.UI.Core.PageAttribute).Assembly,
                typeof(object).Assembly,
            }
            .Concat(AppDomain.CurrentDomain.GetAssemblies())
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => a.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(location => (MetadataReference)MetadataReference.CreateFromFile(location))
            .ToArray();

    private static string Emit(bool designMode)
    {
        var compiler = new ComponentCompiler { TypeAnnotations = false, DesignMode = designMode };
        compiler.SetProjectCompilation(CSharpCompilation.Create(
            "DesignOriginProbe",
            [CSharpSyntaxTree.ParseText(Source, path: "/tmp/Probe.cs")],
            References,
            // Nullable ENABLED: with annotations off, `Action? onPressed` binds as Nullable<Action>,
            // overload resolution silently fails, and every call falls back to the no-model path —
            // which is exactly the path that stamps nothing.
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable)));

        return string.Join("\n", compiler.CompileSource(Source, "/tmp/Probe.cs")
            .Where(r => r.Success)
            .Select(r => r.TypeScript));
    }

    /// <summary>
    /// The default has to be OFF, and off has to mean the emitted module is untouched — the wrapper
    /// is real code, and an SDK build that shipped it would be putting a design tool's concern in
    /// every user's bundle.
    /// </summary>
    [Fact]
    public void DesignModeIsOffByDefault_AndOffOutputMentionsNoOrigin()
    {
        Assert.False(new ComponentCompiler().DesignMode);
        Assert.DoesNotContain("$eq.origin", Emit(designMode: false));
    }

    /// <summary>
    /// The span an origin names must, when applied back to the source, be the construction itself.
    /// This is the whole contract click-to-select rests on: anything that merely CORRELATES would
    /// pass a smoke test and select the wrong line on the first loop.
    /// </summary>
    [Fact]
    public void EveryOriginResolvesToTheConstructionItWasStampedOn()
    {
        var lines = Source.Replace("\r\n", "\n").Split('\n');
        var origins = Regex.Matches(Emit(designMode: true), @"\$eq\.origin\([^""]*""([^""|]+\|[^""]+)""")
            .Select(m => m.Groups[1].Value)
            .ToArray();

        Assert.NotEmpty(origins);

        foreach (var origin in origins)
        {
            // path|startLine:startCol|endLine:endCol, zero-based — the editor's own coordinates.
            var parts = origin.Split('|');
            Assert.Equal(3, parts.Length);
            Assert.Equal("/tmp/Probe.cs", parts[0]);

            var (startLine, startColumn) = Position(parts[1]);
            var (endLine, endColumn) = Position(parts[2]);

            var text = startLine == endLine
                ? lines[startLine][startColumn..endColumn]
                : lines[startLine][startColumn..];

            Assert.Matches(@"^(new\s+)?(Box|Column|Text|Button|Footer)\s*[\(<]", text);
        }
    }

    private static string[] Labels(string js) =>
        Regex.Matches(js, @"\$eq\.origin\([^""]*""[^""]+"", ""([^""]+)""\)")
            .Select(m => m.Groups[1].Value)
            .ToArray();

    /// <summary>
    /// A node carries what to CALL it, so a design tool can label it on hover without asking the
    /// compiler once per pointer move.
    /// </summary>
    [Fact]
    public void EveryStampedNodeCarriesItsComponentName()
    {
        var labels = Labels(Emit(designMode: true));

        Assert.NotEmpty(labels);
        Assert.Contains("Text", labels);
        // Box and Text, not Button: this probe imports Core and Primitives only, so `Button` — which
        // lives in Components — does not resolve, and an unresolved type is deliberately not stamped.
        Assert.Contains("Box", labels);
    }

    /// <summary>A screen assembles half a dozen Columns; the name the author gave one is the only
    /// thing that tells them apart on sight.</summary>
    [Fact]
    public void ANodeAssignedToALocal_IsLabelledWithThatName()
    {
        Assert.Contains("Column \u00B7 column", Labels(Emit(designMode: true)));
    }

    /// <summary>
    /// A helper method's STATIC return type is the abstract node, so labelling by type would call
    /// half a real screen "VisualNode" — true, and useless. The method that built it is both more
    /// informative and where the author has to go to change it.
    /// </summary>
    [Fact]
    public void ANodeFromAHelperMethod_IsLabelledByTheMethod_NotByTheAbstractType()
    {
        var labels = Labels(Emit(designMode: true));

        Assert.Contains("Footer()", labels);
        Assert.DoesNotContain("VisualNode", labels);
    }

    private static (int Line, int Column) Position(string value)
    {
        var parts = value.Split(':');
        return (int.Parse(parts[0]), int.Parse(parts[1]));
    }

    /// <summary>
    /// A node built inside a <c>foreach</c> carries the span of the ONE expression that builds it.
    /// That is the honest answer rather than a limitation: there is no separate source site for the
    /// third row, and the loop body is the only thing anyone could edit.
    /// </summary>
    [Fact]
    public void ANodeBuiltInALoopIsStampedWithTheLoopBodysExpression()
    {
        var js = Emit(designMode: true);
        var loopTextOrigin = Regex.Matches(js, @"\$eq\.origin\(new Text\(row[^""]*""([^""|]+\|[^""]+)""")
            .Select(m => m.Groups[1].Value)
            .SingleOrDefault();

        Assert.NotNull(loopTextOrigin);
        // Line 15 zero-based is the `column.Add(new Text(row, …));` inside the foreach.
        Assert.StartsWith("/tmp/Probe.cs|15:", loopTextOrigin);
    }

    /// <summary>
    /// An origin names a file an EDITOR can open. A relative path would resolve against whatever
    /// working directory that editor happens to have — not the compiler's — so a stamp built from a
    /// relative source path still has to come out rooted.
    /// </summary>
    [Fact]
    public void AnOriginNamesAFullPath_EvenWhenTheSourceWasGivenARelativeOne()
    {
        var compiler = new ComponentCompiler { TypeAnnotations = false, DesignMode = true };
        compiler.SetProjectCompilation(CSharpCompilation.Create(
            "RelativeProbe",
            [CSharpSyntaxTree.ParseText(Source, path: "Screens/Probe.cs")],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable)));

        var js = string.Join("\n", compiler.CompileSource(Source, "Screens/Probe.cs")
            .Where(r => r.Success)
            .Select(r => r.TypeScript));

        var paths = Regex.Matches(js, @"\$eq\.origin\([^""]*""([^|]+)\|")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToArray();

        Assert.NotEmpty(paths);
        Assert.All(paths, path => Assert.True(Path.IsPathRooted(path), $"'{path}' is not rooted"));
    }

    /// <summary>Wrapping must not change what an expression MEANS: the wrapper returns the node, so
    /// a construction still composes into whatever consumed it.</summary>
    [Fact]
    public void StampingLeavesTheSurroundingExpressionIntact()
    {
        var js = Emit(designMode: true);

        // The Box still receives the column as its second argument, wrapper and all.
        Assert.Matches(@"\$eq\.origin\(new Box\(.*column\)", js);
        // And `.add(...)` still runs on the constructed column rather than on a wrapper's result.
        Assert.Contains("column.add(", js);
    }
}
