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
        using System;
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;
        using eQuantic.UI.Primitives;

        public sealed record Money(decimal Amount, string Currency);
        public sealed record Todo(long Id, string Title, Money Price);

        [Page("/wallet")]
        public sealed class Wallet : StatefulComponent, IServerPrefetch
        {
            private decimal _total = 0m;
            private long _count;
            private List<Todo> _todos = new();
            private Dictionary<string, decimal> _rates = new();
            private string _label = "";
            private int _clicks;

            [ServerOnly]
            public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
                => Task.CompletedTask;

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
        // Every value the server carries here, as the manifest lists it: by its wire spec where its
        // JSON form differs from its runtime type, and 'declared' where it does not.
        // A static GETTER, read when the runtime hydrates: a field initializer naming a class runs
        // when this class is defined, and inside the runtime bundle's import cycles that came before
        // the named class existed (a TDZ ReferenceError that stopped the whole bundle loading).
        Assert.Contains("static get $hydration()", result);
        Assert.Contains(
            "return { _clicks: 'declared', _count: 'long', _label: 'declared', _rates: { dict: 'decimal' }, "
            + "_todos: [Todo], _total: 'decimal' };",
            result);
    }

    [Fact]
    public void AComponentTheManifestDoesNotDescribe_ListsNothing()
    {
        // Compiled without the generator there is no manifest, so the server carries nothing to this
        // page and its twin lists nothing to adopt: the two halves read one description.
        var page = new ComponentCompiler().CompileSource(Page, "Wallet.cs").Single(r => r.ComponentName == "Wallet");

        Assert.DoesNotContain("$hydration", page.TypeScript);
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
        Assert.Contains("static get $hydration() { return { id: 'long', price: Money }; }", todo);
        Assert.Contains("static get $hydration() { return { amount: 'decimal' }; }", money);
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

    /// <summary>
    /// A float crosses the wire as the shortest text that names its SINGLE, and JavaScript parses that
    /// text as the nearest double — a different number until it is rounded back. So a float field, a
    /// float? field and a record's float member all carry <c>'single'</c>; a double needs no spec, so a
    /// page lists it as <c>'declared'</c> and a record does not list it.
    /// </summary>
    /// <summary>
    /// A vocabulary value type the runtime ships crosses STRUCTURALLY, since its members are known
    /// here, and NAMES its twin, so the payload is rebuilt on that prototype. Before `'single'` a
    /// Rect needed no spec at all; the first structural spec it got was a plain copy, and a Rect in
    /// a payload lost its getters and methods.
    /// </summary>
    [Fact]
    public void ARuntimeValueType_IsRebuiltOnItsTwin()
    {
        const string source = """
            using eQuantic.UI.Primitives;

            [Page("/frame")]
            public sealed class Frame : StatefulComponent, IServerPrefetch
            {
                private Rect _box;

                [ServerOnly]
                public System.Threading.Tasks.Task PrefetchAsync(System.IServiceProvider services, System.Threading.CancellationToken cancellationToken)
                    => System.Threading.Tasks.Task.CompletedTask;

                public override VisualNode Build(ComponentContext context)
                    => new Text("x", TypeRole.BodyM, context.Theme.TextPrimary);
            }
            """;
        // The vocabulary has to BIND for its members to be known, which takes the project's
        // compilation with Primitives referenced, as eqc builds it.
        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(GeneratedProject.Of(source, "Frame.cs"));
        var results = compiler.CompileSource(source, "Frame.cs");
        var page = results.Single(r => r.ComponentName == "Frame");
        Assert.True(page.Success, string.Join("\n", page.Errors.Select(e => e.Message)));
        Assert.Contains("_box: { of: Rect, members: { x: 'single', y: 'single', width: 'single', height: 'single' } }", page.TypeScript);
    }

    [Fact]
    public void AFloat_HydratesAsASingle_AndADoubleDoesNot()
    {
        const string source = """
            using eQuantic.UI.Primitives;

            public sealed record Reading(float Value, double Precise);

            [Page("/gauge")]
            public sealed class Gauge : StatefulComponent, IServerPrefetch
            {
                [ServerOnly]
                public System.Threading.Tasks.Task PrefetchAsync(System.IServiceProvider services, System.Threading.CancellationToken cancellationToken)
                    => System.Threading.Tasks.Task.CompletedTask;

                private float _level;
                private float? _target;
                private double _exact;
                private Reading _last = new(0, 0);

                public override VisualNode Build(ComponentContext context)
                    => new Text("x", TypeRole.BodyM, context.Theme.TextPrimary);
            }
            """;
        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(GeneratedProject.Of(source, "Gauge.cs"));
        var results = compiler.CompileSource(source, "Gauge.cs");
        var page = results.Single(r => r.ComponentName == "Gauge");
        Assert.True(page.Success, string.Join("\n", page.Errors.Select(e => e.Message)));
        Assert.Contains("return { _exact: 'declared', _last: Reading, _level: 'single', _target: 'single' };", page.TypeScript);
        var reading = results.Single(r => r.ComponentName == "Reading").TypeScript;
        Assert.Contains("static get $hydration() { return { value: 'single' }; }", reading);
    }

    /// <summary>
    /// A dictionary field hydrates into the runtime's dictionary class only where the lowering reads
    /// it as one: the five dictionaries of System.Collections.Generic. Another shape that implements
    /// IDictionary (a ReadOnlyDictionary here) is read by a plain index, which a Dictionary instance
    /// answers with undefined, so it arrives as the plain object JSON made of it (found in review,
    /// #443). A key whose type does not decide carries <c>byValue: 'own'</c>.
    /// </summary>
    [Fact]
    public void ADictionaryField_HydratesIntoTheClassTheLoweringReads()
    {
        const string source = """
            using System.Collections.Generic;
            using System.Collections.ObjectModel;
            using eQuantic.UI.Primitives;

            [Page("/shapes")]
            public sealed class Shapes : StatefulComponent, IServerPrefetch
            {
                [ServerOnly]
                public System.Threading.Tasks.Task PrefetchAsync(System.IServiceProvider services, System.Threading.CancellationToken cancellationToken)
                    => System.Threading.Tasks.Task.CompletedTask;

                private IReadOnlyDictionary<string, int> _read = new Dictionary<string, int>();
                private ReadOnlyDictionary<string, int>? _wrapped;
                private Dictionary<object, int> _any = new();

                public override VisualNode Build(ComponentContext context)
                    => new Text("", TypeRole.BodyM, context.Theme.TextPrimary);
            }
            """;
        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(GeneratedProject.Of(source, "Shapes.cs"));
        var page = compiler.CompileSource(source, "Shapes.cs").Single(r => r.ComponentName == "Shapes");
        Assert.True(page.Success, string.Join("\n", page.Errors.Select(e => e.Message)));
        var map = page.TypeScript.Substring(page.TypeScript.IndexOf("$hydration"));
        Assert.Contains("_read: { dict: null }", map);
        Assert.Contains("_any: { dict: null, byValue: 'own' }", map);
        Assert.Contains("_wrapped: 'declared'", map);
    }

    [Fact]
    public void ACollectionField_HydratesIntoTheClassItsCodeReads()
    {
        // Each one crossed as the array the server writes, into code that asks a Set for `has` or a
        // Queue for `dequeue` (#516).
        const string source = """
            using System;
            using System.Collections.Generic;
            using eQuantic.UI.Primitives;

            [Page("/kept")]
            public sealed class Kept : StatefulComponent, IServerPrefetch
            {
                [ServerOnly]
                public System.Threading.Tasks.Task PrefetchAsync(System.IServiceProvider services, System.Threading.CancellationToken cancellationToken)
                    => System.Threading.Tasks.Task.CompletedTask;

                private HashSet<string> _roles = new();
                private HashSet<long> _ids = new();
                private IReadOnlySet<DateTime> _days = new HashSet<DateTime>();
                private Stack<int> _stack = new();
                private Queue<decimal> _queue = new();
                private LinkedList<string> _list = new();
                private SortedSet<int> _sorted = new();
                private SortedSet<string> _names = new();
                private SortedSet<decimal?> _prices = new();
                private SortedSet<Level> _levels = new();
                private SortedDictionary<string, int> _index = new();
                private SortedList<string, int> _ranks = new();

                public override VisualNode Build(ComponentContext context)
                    => new Text("", TypeRole.BodyM, context.Theme.TextPrimary);
            }

            public enum Level { Low, High }
            """;
        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(GeneratedProject.Of(source, "Kept.cs"));
        var page = compiler.CompileSource(source, "Kept.cs").Single(r => r.ComponentName == "Kept");
        Assert.True(page.Success, string.Join("\n", page.Errors.Select(e => e.Message)));
        var map = page.TypeScript.Substring(page.TypeScript.IndexOf("$hydration"));
        Assert.Contains("_roles: { collection: 'set', of: null }", map);
        Assert.Contains("_ids: { collection: 'set', of: 'long' }", map);
        // A set finds its elements as its element type's default comparer does: a date by value (#531).
        Assert.Contains("_days: { collection: 'set', of: 'dateTime', byValue: true }", map);
        Assert.Contains("_stack: { collection: 'stack', of: null }", map);
        Assert.Contains("_queue: { collection: 'queue', of: 'decimal' }", map);
        Assert.Contains("_list: { collection: 'linkedList', of: null }", map);
        // A sorted one says how its element type orders, which the browser cannot tell from a value: a
        // string in the culture, a decimal by its compareTo, an enum by the values of its members.
        Assert.Contains("_sorted: { collection: 'sortedSet', of: null, order: 'value' }", map);
        Assert.Contains("_names: { collection: 'sortedSet', of: null, order: 'text' }", map);
        Assert.Contains("_prices: { collection: 'sortedSet', of: 'decimal', order: 'comparable' }", map);
        Assert.Contains("_levels: { collection: 'sortedSet', of: null, order: { 'low': 0, 'high': 1 } }", map);
        // And which of the two it is: a SortedList refuses a repeated key in its own words.
        Assert.Contains("_index: { dict: null, sorted: 'dictionary', order: 'text' }", map);
        Assert.Contains("_ranks: { dict: null, sorted: 'list', order: 'text' }", map);
    }

    private static string Compile()
    {
        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(GeneratedProject.Of(Page, "Wallet.cs"));
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
        // exactly as a page library's consumer builds. The references are NAMED, the framework and
        // the vocabulary: the assemblies the test process happens to have loaded depend on the tests
        // that ran before this one, and alone it had not loaded the vocabulary, so IServerPrefetch did
        // not bind and the page had no hydration spec at all.
        var named = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Append(typeof(eQuantic.UI.Primitives.IServerPrefetch).Assembly.Location)
            .Distinct()
            .ToList();
        var processRefs = named
            .Select(path => TestReferences.Of(path))
            .Cast<Microsoft.CodeAnalysis.MetadataReference>()
            .ToList();
        var domainPath = Path.Combine(Path.GetTempPath(), $"acme-domain-{Guid.NewGuid():N}.dll");
        var pagePath = Path.Combine(Path.GetTempPath(), $"acme-page-{Guid.NewGuid():N}");
        try
        {
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

            Directory.CreateDirectory(pagePath);
            File.WriteAllText(Path.Combine(pagePath, "HomePage.cs"), Page);

            var refs = named.Append(domainPath).ToList();
            var compilation = eQuantic.UI.Compiler.Services.ProjectCompilationHelper
                .CreateCompilationFromSources(
                    [Path.Combine(pagePath, "HomePage.cs")], refs, "Acme.Site");

            var compiler = new ComponentCompiler();
            compiler.SetProjectCompilation(GeneratedProject.Generated(compilation));
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
            if (File.Exists(domainPath)) File.Delete(domainPath);
        }
    }
}
