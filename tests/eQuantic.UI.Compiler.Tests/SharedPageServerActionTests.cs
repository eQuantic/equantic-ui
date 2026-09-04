using eQuantic.UI.Compiler;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A [ServerAction] on the WRITE-ONCE page shape: the client twin must be the RPC stub, never the
/// transpiled body. The first real Server Action on a shared page shipped the server's compile
/// service (Stopwatch and all) into the browser, because the shared parse branch never classified
/// the method — found only when the button was pressed. The empty collection-expression field
/// initializer rides along: it must survive as <c>= []</c>, or the first Build that iterates it
/// dies on undefined.
/// </summary>
public class SharedPageServerActionTests
{
    private const string Page = """
        using eQuantic.UI.Primitives;

        [Page("/probe")]
        public sealed class Probe : StatefulComponent
        {
            private string[] _items = [];
            private bool _busy;

            [ServerAction]
            public async Task<string> Compile(string code)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                await Task.Delay(1);
                return code + watch.ElapsedMilliseconds;
            }

            public override VisualNode Build(ComponentContext context)
            {
                var column = new Column(gap: 4);
                foreach (var item in _items)
                    column.Add(new Text(item, TypeRole.BodyM, context.Theme.TextPrimary));
                column.Add(new Button("Go", onPressed: () => Compile("x")));
                return column;
            }
        }
        """;

    [Fact]
    public void ServerActionBody_NeverReachesTheClient()
    {
        var compiler = new ComponentCompiler();
        var result = compiler.CompileSource(Page, "Probe.cs").Single();

        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        // The stub, wired to the action id.
        Assert.Contains("getServerActionsClient().invoke('Probe/Compile'", result.TypeScript);
        // The body stays on the server — none of its guts may survive into the twin.
        Assert.DoesNotContain("Stopwatch", result.TypeScript);
        Assert.DoesNotContain("ElapsedMilliseconds", result.TypeScript);
        Assert.DoesNotContain("elapsedMilliseconds", result.TypeScript);
    }

    [Fact]
    public void EmptyCollectionExpressionField_KeepsItsInitializer()
    {
        var compiler = new ComponentCompiler();
        var result = compiler.CompileSource(Page, "Probe.cs").Single();

        Assert.True(result.Success);
        Assert.Contains("_items", result.TypeScript);
        Assert.Contains("= []", result.TypeScript);
    }
}
