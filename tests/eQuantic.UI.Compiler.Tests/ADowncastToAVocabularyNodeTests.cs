using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A downcast to a node of the vocabulary keeps its type for the checker, whichever class declares the
/// member it reaches (#1396). <c>Pressable</c> inherits <c>Child</c> from <c>SingleChildNode</c>, and <c>Row</c>
/// and <c>Column</c> inherit <c>Children</c> from <c>FlexNode</c>, bases the build keeps on the server; the cast
/// to them was dropped as if they were too, though the runtime carries a twin of each, and the member read off
/// the static type was "Property does not exist on type 'VisualNode'" to <c>tsc</c>, 24 times in the element
/// engine's <c>ChildSlots</c>. <c>Box</c> declares its own, and kept its cast.
/// </summary>
public class ADowncastToAVocabularyNodeTests
{
    private const string Source = """
        using eQuantic.UI.Primitives;

        public static class Slots
        {
            public static VisualNode? Boxed(VisualNode node) => ((Box)node).Child;
            public static VisualNode Pressed(VisualNode node) => ((Pressable)node).Child;
            public static int InARow(VisualNode node) => ((Row)node).Children.Count;
            public static int InAColumn(VisualNode node) => ((Column)node).Children.Count;
        }
        """;

    [Fact]
    public void ADowncastToANodeWhoseMemberIsInherited_KeepsItsType()
    {
        var ts = Compile(Source, "Slots").TypeScript;

        ts.Should().Contain("(node as Box).child", "a member the cast's type declares kept its cast already");
        ts.Should().Contain("(node as Pressable).child", "Pressable inherits Child from SingleChildNode");
        ts.Should().Contain("(node as Row).children", "Row inherits Children from FlexNode");
        ts.Should().Contain("(node as Column).children", "Column inherits Children from FlexNode");
        ts.Should().MatchRegex(@"import \{[^}]*\bPressable\b[^}]*\} from ""@equantic/runtime""",
            "the cast names the runtime's twin, which the module imports from the runtime");
    }

    [Fact]
    public void ADowncastToANodeTheBuildKeepsOnTheServer_WritesTheValueAlone()
    {
        var ts = Compile("""
            using eQuantic.UI.Primitives;

            public static class Flexed
            {
                public static object Of(VisualNode node) => (FlexNode)node;
            }
            """, "Flexed").TypeScript;

        ts.Should().NotContain("as FlexNode", "FlexNode is the server's own base, with no twin to name");
    }

    [Fact]
    public void ADowncastToATypeInsideATypeOfTheRuntimeTheServerKeeps_WritesTheValueAlone()
    {
        // A type of the runtime's namespace declared inside a [ServerOnly] owner has no twin either, whether or
        // not it repeats the attribute (Antigravity's first review of #1399).
        var ts = Compile("""
            using eQuantic.UI.Primitives;

            namespace eQuantic.UI.Primitives
            {
                [ServerOnly]
                public static class Vault
                {
                    public sealed class Key { public int Id { get; init; } }
                }
            }

            public static class Keys
            {
                public static int IdOf(object key) => ((Vault.Key)key).Id;
            }
            """, "Keys").TypeScript;

        ts.Should().NotContain("as Vault", "the owner never crosses, so its nested type has no twin to name");
    }

    private static CompilationResult Compile(string source, string component)
    {
        var path = component + ".cs";
        var tree = CSharpSyntaxTree.ParseText(source, ParseDefaults.Options, path: path);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(file => file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(file => (MetadataReference)TestReferences.Of(file))
            .Append(TestReferences.Of(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        var compilation = CSharpCompilation.Create(component, [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var compiler = new ComponentCompiler { TypeAnnotations = true };
        compiler.SetProjectCompilation(compilation);
        return compiler.CompileSource(source, path).Single(r => r.ComponentName == component);
    }
}
