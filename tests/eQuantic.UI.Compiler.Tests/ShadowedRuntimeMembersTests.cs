using eQuantic.UI.Compiler;
using eQuantic.UI.Compiler.CodeGen;
using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// THE COLLISION #245 OPENS WITH, refused at build time instead of in the browser.
///
/// <para>
/// A C# component's members lower to JavaScript members of the SAME object. C# keeps a capture, a
/// field and a property apart and spells them differently from the runtime's camelCase; JavaScript
/// folds all of it onto one key. So <c>class StatTile(string label, bool centered = false)</c>
/// assigned <c>this.centered</c> over the <c>centered()</c> every component inherited, and
/// <c>AppUI.statTile(...).centered is not a function</c> was the first anyone heard of it — from a
/// published build.
/// </para>
///
/// <para>
/// Two things make this a guard rather than a list. It refuses what the RUNTIME has, read from the
/// live prototype chains (<c>core/runtime-members.spec.ts</c>) into a fixture the compiler embeds,
/// keyed by BASE — <c>Component</c> 3, <c>HtmlElement</c> 6, <c>StatelessComponent</c> 16,
/// <c>StatefulComponent</c> 27. And the set shrank first, because the half that shadowed silently
/// was MOVED rather than diagnosed: the nine DOM properties and fourteen handlers went down to
/// <c>HtmlElement</c> as <c>declare</c>d types, which emit nothing, so they leave the prototype
/// chain altogether; only the two builders they feed are still real members, and they are on
/// <c>HtmlElement</c> where a component never sees them.
/// </para>
/// </summary>
public class ShadowedRuntimeMembersTests
{
    private const string Header =
        "using System; using System.Threading.Tasks; using eQuantic.UI.Primitives; "
        + "using eQuantic.UI.Web.Components; namespace App; ";

    private static CompilationResult Compile(string body) =>
        new ComponentCompiler().CompileSource(Header + body).Single();

    [Theory]
    [InlineData("StatelessComponent", "render")]
    [InlineData("StatelessComponent", "mount")]
    [InlineData("StatelessComponent", "children")]
    [InlineData("StatelessComponent", "serviceProvider")]
    [InlineData("StatefulComponent", "setState")]
    [InlineData("StatefulComponent", "key")]
    public void APrimaryConstructorParameter_MayNotTakeAMemberTheRuntimeUses(string @base, string member)
    {
        var result = Compile($"public class C(string {member}) : {@base} {{ "
                             + "  public override IComponent Build(RenderContext c) => new Text(\"hi\"); }");

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Code.Should().Be("EQ2011");
        result.Errors[0].Message.Should().Contain($"this.{member}",
            "a diagnostic that names only the C# side leaves the reader to work out what it hit — "
            + "the emission is the half they cannot see");
    }

    /// <summary>
    /// AND THE BASE DECIDES, which is the half a union got wrong. `setState`, `key`, `onMount`,
    /// `_sharedStateful` and seven more belong to a STATEFUL component; on a stateless one they
    /// shadow nothing at all, and refusing them is refusing valid code. `buildAttributes` is the
    /// same story one branch over — the escape hatch has it and a component does not.
    /// </summary>
    [Theory]
    [InlineData("setState")]
    [InlineData("key")]
    [InlineData("onMount")]
    [InlineData("buildAttributes")]
    public void AStatelessComponentKeepsTheNamesItsBaseDoesNotHave(string member)
    {
        Compile($"public class C(string {member}) : StatelessComponent {{ "
                + "  public override IComponent Build(RenderContext c) => new Text(\"hi\"); }")
            .Errors.Should().NotContain(error => error.Code == "EQ2011");
    }

    /// <summary>
    /// A SERVER ACTION REACHES THE EMISSION BY A DIFFERENT DOOR. `ParseMethods` skips it on
    /// purpose — its body runs on the server, and transpiling that shipped a `DbContext` to the
    /// browser — and the emitter writes an RPC stub from `ServerActions` instead. Absent from the
    /// member lists, present in the output: the one combination a members-only check cannot see.
    /// Measured, with no diagnostic at all:
    /// <code>
    /// export class Panel extends StatefulComponent {
    ///     async mount() { return await getServerActionsClient().invoke('Panel/Mount', []) }
    /// }
    /// </code>
    /// over the runtime's <c>mount(container)</c> — so the component never mounts, and what
    /// replaced its lifecycle is a round trip to the server.
    /// <para>
    /// Stateful, because that is where it bites: only the stateful arm calls
    /// <c>ParseServerActions</c>, so a <c>[ServerAction]</c> on a stateless component is parsed by
    /// neither path and emits no stub to collide with.
    /// </para>
    /// <para>Mutation: drop the `ServerActions` loop and this fails alone.</para>
    /// </summary>
    [Theory]
    [InlineData("Mount")]
    [InlineData("SetState")]
    public void AServerAction_IsRefusedTheNamesItsStubWouldReplace(string member)
    {
        new ComponentCompiler().CompileSource(Header
                + $"public class Panel : StatefulComponent {{ [ServerAction] public async Task {member}() "
                + "{ await Task.Delay(1); } "
                + "  public override VisualNode Build(ComponentContext c) => new Text(\"hi\"); }")
            .Single(r => r.ComponentName == "Panel").Errors
            .Should().Contain(error => error.Code == "EQ2011",
                "the attribute keeps the BODY on the server; the stub still lands on the same key");
    }

    /// <summary>
    /// AND THE CHAIN DECIDES, not the declared name. An app's own base is ordinary, and matching
    /// only the four names the fixture keys sent every one of them to the bare <c>Component</c>
    /// floor — three names, none of them the lifecycle. Measured, that emitted with no diagnostic
    /// at all:
    /// <code>
    /// export class Child extends MyStatelessBase {
    ///     hydrate() {}
    ///     mount() {}
    /// }
    /// </code>
    /// replacing the two <c>MyStatelessBase</c> inherited, so the component silently never mounts
    /// and never hydrates. Two levels of app base, because one would not have shown that the walk
    /// RECURSES rather than looking once.
    /// <para>Mutation: drop the chain walk and both cases fail; the direct-base cases do not.</para>
    /// </summary>
    [Theory]
    [InlineData("Hydrate", "hydrate")]
    [InlineData("Mount", "mount")]
    public void AComponentOverAnAppBase_IsRefusedWhatTheChainInherits(string member, string lowered)
    {
        var results = new ComponentCompiler().CompileSource(Header
            + "public abstract class MyStatelessBase : StatelessComponent { "
            + "  public override IComponent Build(RenderContext c) => new Text(\"base\"); } "
            + "public abstract class Middle : MyStatelessBase { } "
            + $"public class Child : Middle {{ public void {member}() {{ }} }}");

        results.Single(r => r.ComponentName == "Child").Errors
            .Should().Contain(error => error.Code == "EQ2011" && error.Message.Contains($"`{lowered}"),
                "the base chain is what a component inherits, and the declared name is only its "
                + "first link");
    }

    /// <summary>
    /// The chain keeps the BRANCH, which is the half a walk could have flattened. An app base over
    /// <c>HtmlElement</c> inherits the DOM builders and does NOT inherit <c>mount</c> — so
    /// refusing <c>Mount</c> here would be round five's too-broad bug arriving by a new road.
    /// </summary>
    [Fact]
    public void AnAppBaseOverTheEscapeHatch_KeepsItsSurfaceAndNoOther()
    {
        var results = new ComponentCompiler().CompileSource(Header
            + "public abstract class MyElementBase : HtmlElement { } "
            + "public class Tag : MyElementBase { public void BuildEvents() { } public void Mount() { } }");

        var errors = results.Single(r => r.ComponentName == "Tag").Errors;
        errors.Should().Contain(e => e.Code == "EQ2011" && e.Message.Contains("`buildEvents"),
            "an HtmlElement has the builders, at any depth below it");
        errors.Should().NotContain(e => e.Code == "EQ2011" && e.Message.Contains("`mount("),
            "and it does not have `mount` — a walk that pooled the branches would refuse it");
    }

    /// <summary>A PROPERTY and a FIELD lower to the same key a parameter does, so they are refused
    /// the same way — which is the half the SDK's own library used to sit in ten times.</summary>
    [Fact]
    public void SoMayNoProperty_NorField()
    {
        Compile("public class C : StatelessComponent { public string Render { get; set; } = \"\"; "
                + "  public override IComponent Build(RenderContext c) => new Text(\"hi\"); }")
            .Errors.Should().ContainSingle().Which.Code.Should().Be("EQ2011");

        Compile("public class C : StatelessComponent { private string mount = \"\"; "
                + "  public override IComponent Build(RenderContext c) => new Text(mount); }")
            .Errors.Should().ContainSingle().Which.Code.Should().Be("EQ2011");
    }

    /// <summary>
    /// AN OVERRIDE is exempt, not a method. A component's `Build` is meant to replace the runtime's
    /// `build` — that is how one is written — and refusing it would refuse every component in the
    /// tree.
    /// </summary>
    [Fact]
    public void AnOverrideIsExempt_BecauseReplacingABaseMemberIsWhatOneIsFor()
    {
        // `OnMount` is the case that makes the exemption mean something: it is `protected virtual`
        // on `UiComponent`, a component overrides it to do work on mount, and `onMount` is in the
        // runtime's member list — so without the exemption the guard would refuse the hook the
        // framework exists to offer. (`Build` does NOT exercise it: the parser routes it away from
        // `Methods` entirely, so refusing every method there breaks no test — measured.)
        Compile("public class C : StatelessComponent { "
                + "  protected override void OnMount() { } "
                + "  public override IComponent Build(RenderContext c) => new Text(\"hi\"); }")
            .Errors.Should().NotContain(error => error.Code == "EQ2011");
    }

    /// <summary>
    /// AND A PLAIN METHOD IS NOT. `public void Mount()` overrides nothing on the component bases —
    /// it is a new method that happens to be spelled like one the runtime has — and the emission
    /// does not know the difference:
    /// <code>
    /// export class C extends StatelessComponent {
    ///     build(_context: BuildContext) { return new Text('hi'); }
    ///     mount() {}
    /// }
    /// </code>
    /// `mount()` REPLACES the runtime's `mount(container)`, so the component never mounts and
    /// nothing says why. The first version of this guard skipped methods entirely, and a test of
    /// its own asserted that `Mount` should be allowed — measured, and it was asserting the defect.
    /// </summary>
    [Fact]
    public void AMethodThatOverridesNothingIsRefused_LikeAnyOtherMember()
    {
        var result = Compile("public class C : StatelessComponent { "
                             + "  public void Mount() { } "
                             + "  public override IComponent Build(RenderContext c) => new Text(\"hi\"); }");

        result.Errors.Should().ContainSingle().Which.Code.Should().Be("EQ2011");
        result.Errors[0].Message.Should().Contain("mount()",
            "a method lowers to a method, and the message shows the shape that collided");
        result.Errors[0].Message.Should().Contain("override",
            "the fix is a rename OR an override, and a diagnostic that names only the rename hides "
            + "half of it");
    }

    /// <summary>
    /// `Constructor` is a legal C# name and lowers like any other member — `this.constructor =
    /// constructor`, over the instance's own class. The runtime reads `target.constructor.$hydration`
    /// to adopt server state and `this.constructor.name` when a render fails, so a component that
    /// took the name would hydrate as nothing and report a failure it could not name. It looks like
    /// plumbing rather than a member, which is exactly why the fixture keeps it.
    /// </summary>
    [Fact]
    public void EvenConstructor_WhichLooksLikePlumbingAndIsAMember()
    {
        var result = Compile("public class C(string constructor) : StatelessComponent { "
                             + "  public override IComponent Build(RenderContext c) => new Text(constructor); }");

        result.Errors.Should().ContainSingle().Which.Code.Should().Be("EQ2011");
    }

    /// <summary>
    /// THE SHARPEST CASE, and the one first excluded as impossible: a leading underscore lowers
    /// UNCHANGED (`IdentifierStrategy` returns `this._name` for it), and `_`-prefixed is how
    /// component state is written all over this repository. So the runtime's own
    /// `_mounted`, `_renderManager`, `_instances`, `_scheduleRender` and the rest are names a C#
    /// field reaches exactly — and what a collision corrupts there is the LIFECYCLE, silently: the
    /// component stops mounting, or renders twice, or never releases.
    /// <para>
    /// The fixture excluded every `_` name on the reasoning that no C# member lowers to one, which
    /// is the opposite of true.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("_mounted")]
    [InlineData("_renderManager")]
    [InlineData("_instances")]
    [InlineData("_scheduleRender")]
    public void TheRuntimesOwnPrivateState_IsReachableByAFieldAndRefused(string member)
    {
        var result = Compile($"public class C : StatelessComponent {{ private int {member} = 0; "
                             + $"  public override IComponent Build(RenderContext c) => new Text({member}.ToString()); }}");

        result.Errors.Should().ContainSingle().Which.Code.Should().Be("EQ2011");
        result.Errors[0].Message.Should().Contain($"this.{member}");
    }

    /// <summary>
    /// And ORDINARY `_`-prefixed state is untouched — `_count`, `_series`, `_hidden` are how this
    /// repository's own components are written, and a guard that refused the idiom would be
    /// unshippable. Only the names the runtime actually uses are refused.
    /// </summary>
    [Fact]
    public void OrdinaryUnderscoreState_IsNotRefused()
    {
        Compile("public class C : StatelessComponent { private int _count = 0; private bool _hidden = false; "
                + "  public override IComponent Build(RenderContext c) => new Text(_count.ToString()); }")
            .Errors.Should().NotContain(error => error.Code == "EQ2011");
    }

    /// <summary>
    /// An ordinary component compiles. The cheapest way for a guard like this to go wrong is to be
    /// too wide, and a green suite would not say so — every other test here asks it to fire.
    /// </summary>
    [Fact]
    public void AnOrdinaryComponentIsUntouched()
    {
        var result = Compile("public class C(string label, bool compact = false) : StatelessComponent { "
                             + "  public override IComponent Build(RenderContext c) => new Text(label); }");

        result.Errors.Should().NotContain(error => error.Code == "EQ2011");
    }

    /// <summary>
    /// The list is not typed here, and this is the half that says so: it comes from
    /// <c>Resources/runtime-members.txt</c>, which <c>core/runtime-members.spec.ts</c> writes by
    /// walking the live runtime. What this pins is that the fixture TRAVELS — embedded in the
    /// assembly, because eqc ships as <c>tools/net10.0/eqc.dll</c> and a file beside the source
    /// tree is not there when a consumer builds.
    /// </summary>
    [Fact]
    public void TheListIsEmbedded_AndComesFromTheRuntime()
    {
        var all = ShadowedRuntimeMembers.All;
        all.Should().NotBeEmpty(
            "an empty fixture is a guard that refuses nothing, and it would pass every test above "
            + "that asks for a rename");
        all.Keys.Should().BeEquivalentTo(
            ["Component", "HtmlElement", "StatelessComponent", "StatefulComponent"],
            "one union of the bases refuses a stateful member on a stateless component, where it "
            + "shadows nothing at all");

        all["StatelessComponent"].Should().Contain(["render", "mount", "children"]);
        // This case used to assert the OPPOSITE — that no `_` name is in the list, "because no C#
        // member lowers to one". `IdentifierStrategy` lowers a leading underscore unchanged, so
        // every one of them is reachable by a C# field, and a test asserting they were absent was
        // pinning the hole rather than the guard.
        all["StatelessComponent"].Should().Contain(["_mounted", "_renderManager", "_instances"],
            "a leading underscore lowers unchanged, and what these corrupt is the lifecycle");

        // The eleven a stateful component adds, and the surface only the escape hatch carries.
        all["StatefulComponent"].Should().Contain(["setState", "key", "_sharedStateful"]);
        all["StatelessComponent"].Should().NotContain(["setState", "key", "_sharedStateful"]);
        all["HtmlElement"].Should().Contain(["buildAttributes", "buildEvents"]);
        all["Component"].Should().NotContain(["buildAttributes", "buildEvents"]);
    }

    /// <summary>
    /// ONE RULE FOR "THE RUNTIME PROVIDES THIS", because two readers have to agree about it: the
    /// extension-home lowering asks it whether to emit a qualified call, and `RegisterIntroduced`
    /// asks it where to import that call's home from. They were two rules — an attribute check and
    /// a namespace check — and a home marked `[RuntimeProvided]` OUTSIDE the runtime namespaces
    /// answered yes to one and no to the other: emitted as `Home.method(…)`, bucketed as an app
    /// type, and written as no module either, because the parser skips runtime-provided classes.
    /// The call would have named nothing at all.
    /// <para>
    /// The attribute's own doc is what makes that case real rather than hypothetical — it exists to
    /// "extend it to runtime-backed types living elsewhere — e.g. the web adapter
    /// <c>VisualNodeComponent</c>".
    /// </para>
    /// </summary>
    [Fact]
    public void TheAttributeSaysRuntimeProvided_WhereverTheTypeLives()
    {
        const string source = """
            namespace App.Elsewhere;

            public sealed class RuntimeProvidedAttribute : System.Attribute;

            [RuntimeProvided]
            public static class WebHelpers;

            public static class PlainHelpers;
            """;
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create("RuleProbe", [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var model = compilation.GetSemanticModel(tree);

        INamedTypeSymbol Named(string name) => tree.GetRoot().DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Select(declaration => model.GetDeclaredSymbol(declaration)!)
            .Single(symbol => symbol.Name == name);

        Named("WebHelpers").IsRuntimeProvided().Should().BeTrue(
            "the attribute says so, and a namespace outside the runtime's own does not unsay it");
        Named("PlainHelpers").IsRuntimeProvided().Should().BeFalse(
            "an ordinary app class is the app's to emit");
    }
}
