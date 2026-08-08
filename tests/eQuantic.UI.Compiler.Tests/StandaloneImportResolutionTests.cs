using eQuantic.UI.Compiler;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// Import resolution in STANDALONE CompileSource (no references, no semantic model — the
/// playground's mode): the source text is the user's whole universe, so referenced types it does
/// not declare must import from <c>@equantic/runtime</c>, never from invented <c>./X</c> modules.
/// The first version of the plain-JS test only pinned the runtime import line and let
/// <c>import { Button } from "./Button"</c> slide by — a module that exists nowhere.
/// </summary>
public class StandaloneImportResolutionTests
{
    [Fact]
    public void VocabularyTypes_ImportFromTheRuntime_NotFromInventedModules()
    {
        var compiler = new ComponentCompiler { TypeAnnotations = false };
        var result = compiler.CompileSource("""
            using eQuantic.UI.Core;
            using eQuantic.UI.Primitives;

            public sealed class Probe : StatefulComponent
            {
                private int _count;

                public override VisualNode Build(ComponentContext context)
                {
                    var column = new Column(gap: Space.S3);
                    column.Add(new Text($"Count: {_count}", TypeRole.Display, context.Theme.TextPrimary));
                    column.Add(new Button("Up", onPressed: () => SetState(() => _count++)));
                    return column;
                }
            }
            """, "Probe.cs").Single();

        Assert.True(result.Success);
        var imports = result.TypeScript.Split('\n').Where(l => l.StartsWith("import")).ToArray();
        Assert.Contains(imports, l => l.Contains("\"@equantic/runtime\"")
            && l.Contains("Button") && l.Contains("Column") && l.Contains("Text"));
        Assert.DoesNotContain(imports, l => l.Contains("\"./"));
    }

    [Fact]
    public void ATypeDeclaredInTheSource_StaysAUserModule()
    {
        // The set is the SOURCE's declarations, not a fixed list: a class the user wrote next to
        // the page is still theirs, and still imports as its own module.
        var compiler = new ComponentCompiler { TypeAnnotations = false };
        var results = compiler.CompileSource("""
            using eQuantic.UI.Core;
            using eQuantic.UI.Primitives;

            public sealed class Badge : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) =>
                    new Text("hi", TypeRole.Label, context.Theme.TextPrimary);
            }

            public sealed class Host : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context)
                {
                    var column = new Column(gap: 4);
                    column.Add(new Badge());
                    return column;
                }
            }
            """, "Pair.cs");

        var host = results.Single(r => r.ComponentName == "Host");
        Assert.True(host.Success);
        Assert.Contains("from \"./Badge\"", host.TypeScript);
    }

    [Fact]
    public void WithReferences_TheSemanticPathStaysAuthoritative()
    {
        // A semantic model present means ResolvedSemantically — the standalone fallback must not
        // reroute anything the scanner already classified.
        var compiler = new ComponentCompiler();
        var result = compiler.CompileSource("""
            using eQuantic.UI.Core;
            using eQuantic.UI.Primitives;

            public sealed class Probe : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) =>
                    new Text("hi", TypeRole.Label, context.Theme.TextPrimary);
            }
            """, "Probe.cs").Single();

        Assert.True(result.Success);
        Assert.DoesNotContain("from \"./Text\"", result.TypeScript);
    }
}
