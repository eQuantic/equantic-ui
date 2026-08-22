using eQuantic.UI.Compiler;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// Fase 5, slice 7 — the TYPED BOUNDARY. The compiler knows the C# type of every state field and
/// every Server Action's return, so it writes that knowledge into the twin: a
/// <c>static $hydration</c> map naming each field whose wire form differs from its runtime type
/// (long crosses as a string, decimal as a string, records as plain objects), and an
/// <c>$eq.hydrate</c> around every action result that needs one. The runtime coerces ONCE at the
/// boundary — which is what lets the defensive per-use <c>$eq.num.dec/long</c> wraps go.
/// </summary>
public class HydrationSpecEmissionTests
{
    private const string Page = """
        using System.Collections.Generic;
        using System.Threading.Tasks;
        using eQuantic.UI.Core;
        using eQuantic.UI.Primitives;

        public sealed record Money(decimal Amount, string Currency);
        public sealed record Todo(long Id, string Title, Money Price);

        [Page("/wallet")]
        public sealed class Wallet : StatefulComponent
        {
            private decimal _total = 0m;
            private long _count;
            private List<Todo> _todos = new();
            private Dictionary<string, decimal> _rates = new();
            private string _label = "";
            private int _clicks;

            [ServerAction]
            public async Task<decimal> LoadTotal() { await Task.Delay(1); return 1.5m; }

            [ServerAction]
            public async Task<List<Todo>> LoadTodos() { await Task.Delay(1); return new(); }

            [ServerAction]
            public async Task<string> LoadLabel() { await Task.Delay(1); return "x"; }

            public override VisualNode Build(ComponentContext context)
                => new Text(_label, TypeRole.BodyM, context.Theme.TextPrimary);
        }
        """;

    [Fact]
    public void StateFields_CarryTheirHydrationMap()
    {
        var result = Compile();
        // Every field whose wire form differs — and none of the identity ones.
        Assert.Contains("static $hydration = { _total: 'decimal', _count: 'long', _todos: [Todo], _rates: { dict: 'decimal' } };", result);
        Assert.DoesNotContain("_label:", result.Substring(result.IndexOf("$hydration")));
        Assert.DoesNotContain("_clicks:", result.Substring(result.IndexOf("$hydration")));
    }

    [Fact]
    public void ActionResults_HydrateByTheReturnTypeSpec()
    {
        var result = Compile();
        Assert.Contains("return $eq.hydrate(await getServerActionsClient().invoke('Wallet/LoadTotal', []), 'decimal')", result);
        Assert.Contains("return $eq.hydrate(await getServerActionsClient().invoke('Wallet/LoadTodos', []), [Todo])", result);
        // An identity return stays a bare invoke — the common case keeps its shape.
        Assert.Contains("return await getServerActionsClient().invoke('Wallet/LoadLabel', [])", result);
    }

    [Fact]
    public void RecordTwins_CarryTheirOwnMap()
    {
        var compiler = new ComponentCompiler();
        var results = compiler.CompileSource(Page, "Wallet.cs");
        var todo = results.Single(r => r.ComponentName == "Todo").TypeScript;
        var money = results.Single(r => r.ComponentName == "Money").TypeScript;
        // The member that hydrates, by its camelCased twin name; nested records point at the class.
        Assert.Contains("static $hydration = { id: 'long', price: Money }", todo);
        Assert.Contains("static $hydration = { amount: 'decimal' }", money);
        // The map is the only runtime mention of Money in Todo's module — the import must follow.
        Assert.Contains("import { Money } from \"./Money\";", todo);
    }

    [Fact]
    public void CompatFields_DefaultToTheirRuntimeType()
    {
        // `long _count;` (no initializer) must default 0-as-BigInt: the default IS the field's
        // runtime type — both for arithmetic before any hydration and as the witness legacy
        // payloads are typed by.
        var result = Compile();
        Assert.Contains("$eq.num.long(0)", result);
    }

    private static string Compile()
    {
        var compiler = new ComponentCompiler();
        var results = compiler.CompileSource(Page, "Wallet.cs");
        var page = results.Single(r => r.ComponentName == "Wallet");
        Assert.True(page.Success, string.Join("\n", page.Errors.Select(e => e.Message)));
        return page.TypeScript;
    }
}
