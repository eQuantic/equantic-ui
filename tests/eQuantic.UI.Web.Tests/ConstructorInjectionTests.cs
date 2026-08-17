using eQuantic.UI.Compiler;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A page takes what it needs through its constructor — a photo library, a ledger, a configuration
/// — and natively ActivatorUtilities gives it. In the browser the constructor has to resolve it
/// itself, or "the same page on all four targets" stops being true exactly where an app stops being
/// a drawing and starts doing something.
/// <para>
/// The rule is asked of the MODEL, not guessed from a name: a constructor parameter whose type is
/// an INTERFACE is a dependency; everything else is data the caller passes. A component takes what
/// it draws (a label, a variant, a callback) and none of those is ever an interface.
/// </para>
/// </summary>
public class ConstructorInjectionTests
{
    private const string PageSource = """
        using eQuantic.UI.Components;
        using eQuantic.UI.Primitives;

        namespace eQuantic.UI.Web.Tests.Fixtures;

        public sealed class DependentPage : StatefulComponent
        {
            private readonly IPhotoLibrary _library;

            public DependentPage(IPhotoLibrary library, string title = "Pick")
            {
                _library = library;
            }

            public override VisualNode Build(ComponentContext context) =>
                new Button(title: "Choose");
        }
        """;

    private static string Transpile(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: "DependentPage.cs");
        var usings = CSharpSyntaxTree.ParseText(
            "global using System;\nglobal using System.Collections.Generic;\nglobal using System.Linq;",
            path: "GlobalUsings.g.cs");

        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));

        var compilation = CSharpCompilation.Create("Injection", [tree, usings], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        return compiler.CompileSource(source, "DependentPage.cs").Single().TypeScript;
    }

    [Fact]
    public void ADependency_IsResolved_NotPassedIn()
    {
        var page = Transpile(PageSource);

        page.Should().Contain("$eq.services.resolve('IPhotoLibrary')");
        page.Should().NotContain("library?: any", "nobody passes a photo library to a page");
    }

    /// <summary>
    /// The clock crosses like any other capability, and the SUBSCRIPTION crosses with it: a
    /// component that starts a timer in OnMount and disposes it in OnUnmount must arrive in the
    /// browser having done both, or every page navigation leaves a timer running against a
    /// component nobody can see.
    /// </summary>
    [Fact]
    public void AClockSubscription_CrossesWithItsDisposal()
    {
        var ticking = Transpile("""
            using eQuantic.UI.Components;
            using eQuantic.UI.Primitives;

            namespace eQuantic.UI.Web.Tests.Fixtures;

            public sealed class TickingPage : StatefulComponent
            {
                private readonly IClock _clock;
                private IDisposable? _tick;
                private int _step;

                public TickingPage(IClock clock)
                {
                    _clock = clock;
                }

                protected override void OnMount() =>
                    _tick = _clock.Every(TimeSpan.FromMilliseconds(1700),
                        () => SetState(() => _step = (_step + 1) % 4));

                protected override void OnUnmount() => _tick?.Dispose();

                public override VisualNode Build(ComponentContext context) =>
                    new Text($"step {_step}", TypeRole.BodyM);
            }
            """);

        ticking.Should().Contain("$eq.services.resolve('IClock')");
        ticking.Should().Contain("onMount()");
        ticking.Should().Contain(".every(");
        ticking.Should().Contain("onUnmount()");
        ticking.Should().Contain(".dispose()");
        // The interval is the runtime's TimeSpan, which is what the web realization reads the
        // milliseconds off. A bare number here would mean the two sides disagree about the unit.
        ticking.Should().Contain("timeSpan.fromMilliseconds(1700)");
    }

    [Fact]
    public void TheResolvedModule_ImportsTheRuntimeNamespaceItUses()
    {
        // Emitting any `$eq.*` requires the module to import `$eq`. This one emitted the resolve
        // line and no import: every page taking a dependency died on "$eq is not defined" the
        // moment it was constructed — the whole feature, dead in a browser, green in the suite.
        Transpile(PageSource).Should().Contain("$eq").And.MatchRegex(@"import \{[^}]*\$eq[^}]*\} from ""@equantic/runtime""");
    }

    [Fact]
    public void DataParameters_StayExactlyWhereTheyWere()
    {
        Transpile(PageSource).Should().Contain("title: any = 'Pick'");
    }

    [Fact]
    public void ItResolves_BEFORE_TheConstructorBodyThatUsesIt()
    {
        var page = Transpile(PageSource);

        var resolved = page.IndexOf("$eq.services.resolve", StringComparison.Ordinal);
        var used = page.IndexOf("this._library = library", StringComparison.Ordinal);

        resolved.Should().BeGreaterThan(-1);
        used.Should().BeGreaterThan(resolved, "the body can only use what is already resolved");
    }

    [Fact]
    public void AComponentTakingOnlyData_IsUntouched()
    {
        // The rule must not reach a component: it takes what it draws, and every one of those is
        // passed by whoever writes the tree.
        var component = Transpile("""
            using eQuantic.UI.Components;
            using eQuantic.UI.Primitives;

            namespace eQuantic.UI.Web.Tests.Fixtures;

            public sealed class Plain : StatelessComponent
            {
                public Plain(string label, Variant variant = Variant.Primary) { }

                public override VisualNode Build(ComponentContext context) => new Text("x", TypeRole.BodyM);
            }
            """);

        component.Should().Contain("label?: any").And.Contain("variant: any = 'primary'");
        component.Should().NotContain("$eq.services.resolve");
    }

    [Fact]
    public void ACollectionInterface_IsDATA_NotADependency()
    {
        // The first version of this rule took `IReadOnlyList<T>` for a service, and an Accordion
        // resolving its own rows from a container is nonsense. The committed transpilation caught
        // it within the hour; this keeps it caught.
        var component = Transpile("""
            using eQuantic.UI.Components;
            using eQuantic.UI.Primitives;

            namespace eQuantic.UI.Web.Tests.Fixtures;

            public sealed class Listing : StatelessComponent
            {
                public Listing(IReadOnlyList<string> rows, IEnumerable<int> counts) { }

                public override VisualNode Build(ComponentContext context) => new Text("x", TypeRole.BodyM);
            }
            """);

        component.Should().NotContain("$eq.services.resolve");
        component.Should().Contain("rows?: any").And.Contain("counts?: any");
    }
}
