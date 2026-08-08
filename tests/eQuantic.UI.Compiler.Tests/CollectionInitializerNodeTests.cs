using eQuantic.UI.Compiler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A COLLECTION initializer on a node (`new Column(gap) { a, b }`) is C#'s Add-per-element — it must
/// lower to <c>add()</c> calls on the constructed twin. It used to ride the trailing CONFIG slot as
/// a raw array (<c>new Column(12, [a, b])</c>), where the twin Object.assigns the array's INDICES
/// onto the node: <c>children</c> stayed empty, SSR rendered the children, hydration dropped them —
/// silently, in both compilation modes. Found writing the declarative form the docs advertise.
/// </summary>
public class CollectionInitializerNodeTests
{
    private const string DeclarativePage = """
        using eQuantic.UI.Primitives;

        namespace Demo;

        public sealed class Decl : StatefulComponent
        {
            private int _count;

            public override VisualNode Build(ComponentContext context) =>
                new Column(gap: Space.S3)
                {
                    new Text($"Count: {_count}", TypeRole.Display, context.Theme.TextPrimary),
                    new Spacer(),
                };
        }
        """;

    private static CompilationResult CompileWithRefs(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: "Probe.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        var compilation = CSharpCompilation.Create("Probe", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        return compiler.CompileSource(source, "Probe.cs").Single();
    }

    [Fact]
    public void NodeCollectionInitializer_WithRefs_LowersToAddCalls()
    {
        var result = CompileWithRefs(DeclarativePage);

        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        // Add-per-element, exactly C#'s semantics — never the config slot.
        Assert.Contains("$n.add(new Text(", result.TypeScript);
        Assert.Contains("$n.add(new Spacer())", result.TypeScript);
        Assert.DoesNotContain("new Column(12, [", result.TypeScript);
        Assert.DoesNotContain("Object.assign(new Column", result.TypeScript);
    }

    [Fact]
    public void NodeCollectionInitializer_Standalone_LowersToAddCalls()
    {
        var result = new ComponentCompiler().CompileSource(DeclarativePage, "Decl.cs").Single();

        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        Assert.Contains("$n.add(new Text(", result.TypeScript);
        Assert.Contains("$n.add(new Spacer())", result.TypeScript);
        Assert.DoesNotContain("Object.assign(new Column", result.TypeScript);
    }

    [Fact]
    public void TargetTypedNodeCollectionInitializer_LowersToAddCalls()
    {
        var result = CompileWithRefs("""
            using eQuantic.UI.Primitives;

            namespace Demo;

            public sealed class Decl : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context)
                {
                    Column column = new(4) { new Spacer() };
                    return column;
                }
            }
            """);

        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        Assert.Contains("$n.add(new Spacer())", result.TypeScript);
        Assert.Contains("new Column(4)", result.TypeScript);
    }

    [Fact]
    public void ListAndDictionaryInitializers_KeepTheirLiteralLowering()
    {
        var result = CompileWithRefs("""
            using System.Collections.Generic;
            using eQuantic.UI.Primitives;

            namespace Demo;

            public sealed class Decl : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context)
                {
                    var names = new List<string> { "a", "b" };
                    var ranks = new Dictionary<string, int> { { "a", 1 } };
                    return new Text(names[0] + ranks.Count, TypeRole.BodyM);
                }
            }
            """);

        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        Assert.Contains("['a', 'b']", result.TypeScript);
        Assert.Contains("'a': 1", result.TypeScript);
        Assert.DoesNotContain("$n.add('a')", result.TypeScript);
    }
}
