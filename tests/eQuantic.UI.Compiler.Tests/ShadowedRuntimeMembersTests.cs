using eQuantic.UI.Compiler;
using eQuantic.UI.Compiler.Services;
using FluentAssertions;

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
/// live prototype chain (<c>core/runtime-members.spec.ts</c>) into a fixture the compiler embeds;
/// and the set shrank from 46 to 17 first, because the half that shadowed silently — nine DOM
/// fields and fourteen handlers nothing read — was deleted rather than diagnosed.
/// </para>
/// </summary>
public class ShadowedRuntimeMembersTests
{
    private const string Header =
        "using System; using eQuantic.UI.Primitives; using eQuantic.UI.Web.Components; namespace App; ";

    private static CompilationResult Compile(string body) =>
        new ComponentCompiler().CompileSource(Header + body).Single();

    [Theory]
    [InlineData("render")]
    [InlineData("setState")]
    [InlineData("mount")]
    [InlineData("children")]
    [InlineData("serviceProvider")]
    public void APrimaryConstructorParameter_MayNotTakeAMemberTheRuntimeUses(string member)
    {
        var result = Compile($"public class C(string {member}) : StatelessComponent {{ "
                             + "  public override IComponent Build(RenderContext c) => new Text(\"hi\"); }");

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Code.Should().Be("EQ2011");
        result.Errors[0].Message.Should().Contain($"this.{member}",
            "a diagnostic that names only the C# side leaves the reader to work out what it hit");
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
    /// And OVERRIDING is not shadowing. A component's `Build` is meant to replace the runtime's
    /// `build` — that is how one is written — so a method is never refused. Without this case the
    /// rule above would refuse every component in the tree, which is a guard nobody could ship.
    /// </summary>
    [Fact]
    public void AMethodIsNeverRefused_BecauseOverridingIsTheWholePoint()
    {
        Compile("public class C : StatelessComponent { "
                + "  public void Mount() { } "
                + "  public override IComponent Build(RenderContext c) => new Text(\"hi\"); }")
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
        ShadowedRuntimeMembers.All.Should().NotBeEmpty(
            "an empty fixture is a guard that refuses nothing, and it would pass every test above "
            + "that asks for a rename");
        ShadowedRuntimeMembers.All.Should().Contain(["render", "setState", "children"]);
        ShadowedRuntimeMembers.All.Should().NotContain(name => name.StartsWith("_", StringComparison.Ordinal),
            "the runtime's internals are spelled with a leading underscore and no C# member lowers to one");
    }
}
