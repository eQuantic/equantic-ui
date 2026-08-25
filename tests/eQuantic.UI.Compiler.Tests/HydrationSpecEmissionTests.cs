using System;
using System.IO;
using System.Linq;
using eQuantic.UI.Compiler;
using FluentAssertions;
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

/// <summary>
/// The STRUCTURAL half of the boundary: a domain record from a REFERENCED assembly — the ordinary
/// shape of a page library — has no twin to name, so its spec names the members instead. Found by
/// the web site: an array of foreign records carried its longs as the strings EqJson wrote, and the
/// first division met "Cannot mix BigInt and other types", in the browser only.
/// </summary>
public class ForeignRecordHydrationTests
{
    private const string Domain = """
        namespace Acme.Domain;

        public enum PackageCategory { Data, Web }

        public sealed record PackageSummary(
            string Id, string Version, long Downloads, PackageCategory Category)
        {
            public bool IsPrerelease => Version.Contains('-');
        }

        public sealed record Movers(PackageSummary Rising, PackageSummary Falling);
        """;

    private const string Page = """
        using System.Threading.Tasks;
        using Acme.Domain;
        using eQuantic.UI.Core;
        using eQuantic.UI.Primitives;

        [Page("/home")]
        public sealed class HomePage : StatelessComponent, IServerPrefetch
        {
            private long _downloads = 790_000;
            private PackageSummary[] _top = [];
            private PackageSummary[] _alsoTop = [];
            private Movers? _movers;

            [ServerOnly]
            public Task PrefetchAsync(System.IServiceProvider services, System.Threading.CancellationToken ct)
                => Task.CompletedTask;

            public override VisualNode Build(ComponentContext context)
                => new Text($"{_downloads}", TypeRole.BodyM);
        }
        """;

    [Fact]
    public void AForeignRecordsMembersKeepTheBoundaryTyped()
    {
        // The domain assembly is REAL metadata, not source — compiled here and referenced by path,
        // exactly as a page library's consumer builds.
        var processRefs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(a.Location))
            .Cast<Microsoft.CodeAnalysis.MetadataReference>()
            .ToList();
        var domainPath = Path.Combine(Path.GetTempPath(), $"acme-domain-{Guid.NewGuid():N}.dll");
        var domain = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(
                "Acme.Domain",
                [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(Domain)],
                processRefs,
                new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(
                    Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));
        using (var output = File.Create(domainPath))
            domain.Emit(output).Success.Should().BeTrue(
                string.Join("; ", domain.GetDiagnostics()
                    .Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)));

        var pagePath = Path.Combine(Path.GetTempPath(), $"acme-page-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(pagePath);
            File.WriteAllText(Path.Combine(pagePath, "HomePage.cs"), Page);

            var refs = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => a.Location)
                .Append(domainPath)
                .Distinct()
                .ToList();
            var compilation = eQuantic.UI.Compiler.Services.ProjectCompilationHelper
                .CreateCompilationFromSources(
                    [Path.Combine(pagePath, "HomePage.cs")], refs, "Acme.Site");

            var compiler = new ComponentCompiler();
            compiler.SetProjectCompilation(compilation);
            var twin = compiler.CompileDirectory(pagePath)
                .Single(r => r.ComponentName == "HomePage").TypeScript;

            // The scalar keeps its tag, and the foreign array gets the STRUCTURAL spec — member
            // names camelCased exactly as EqJson writes them, the computed property absent (it is
            // not a payload slot), no import of a module that exists nowhere.
            twin.Should().Contain("_downloads: 'long'");
            twin.Should().Contain("_top: [{ members: { downloads: 'long' } }]");
            twin.Should().NotContain("from \"./PackageSummary\"");

            // A second FIELD of the same foreign type gets its own walk (the public overload
            // starts a fresh visiting set per field), so it was never at risk — pinned anyway.
            twin.Should().Contain("_alsoTop: [{ members: { downloads: 'long' } }]");

            // The shape that WAS at risk: two members of the same foreign type inside ONE spec
            // share the walk's visiting set. The guard is a recursion STACK, not a memo — left
            // marked after the first member, the second silently got no spec at all.
            twin.Should().Contain(
                "_movers: { members: { rising: { members: { downloads: 'long' } }, falling: { members: { downloads: 'long' } } } }");
        }
        finally
        {
            if (Directory.Exists(pagePath)) Directory.Delete(pagePath, recursive: true);
            File.Delete(domainPath);
        }
    }
}
