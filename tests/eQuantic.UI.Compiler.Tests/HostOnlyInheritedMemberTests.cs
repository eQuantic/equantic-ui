using System.Linq;
using eQuantic.UI.Compiler;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A member a component reaches through a node that CROSSES, declared on a base that does not.
///
/// <para>
/// #226 made the host-only fence receiver-aware for exactly this: <c>SingleChildNode</c> is
/// <c>[ServerOnly]</c>, and without the receiver a component reading <c>pressable.Child</c> — a
/// property every wrapper's runtime twin carries — was refused with advice that made no sense for
/// reading a node's child. The fix asks what type the member was reached THROUGH.
/// </para>
///
/// <para>
/// IT WAS WIRED AT THE MEMBER-ACCESS SITE ONLY, and #228 is what found that. Fencing
/// <c>FlexNode</c> turned every <c>Add</c> in the shared component library red — 29 diagnostics,
/// sixteen tests across three suites — because an INVOCATION took a different path and asked the
/// fence without a receiver. A hole that opens only for the next host-only type with an inherited
/// METHOD is a hole nobody sees; <c>SingleChildNode</c>'s members are properties, so nothing had
/// ever exercised it.
/// </para>
///
/// <para>
/// A WHOLE COMPILATION rather than a statement probe, and that is not decoration: measured, the
/// statement probe in <c>HostOnlyFrameworkTypeTests</c> reports NOTHING for <c>row.Add(child)</c>
/// with the fix reverted, so a guard written there would have passed either way. The condition is a
/// component's <c>Build</c>, which is where the 29 came from.
/// </para>
/// </summary>
public class HostOnlyInheritedMemberTests
{
    private static CompilationResult CompileBuild(string body)
    {
        var source = $$"""
            using eQuantic.UI.Primitives;
            using static eQuantic.UI.Components.UI;

            namespace Demo;

            public sealed class Probe : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context)
                {
                    {{body}}
                }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(source, path: "Probe.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(Primitives.VisualNode).Assembly.Location))
            .Append(MetadataReference.CreateFromFile(typeof(Components.Button).Assembly.Location));
        var compilation = CSharpCompilation.Create("InheritedMemberProbe", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        compilation.GetTypeByMetadataName("eQuantic.UI.Primitives.FlexNode")
            .Should().NotBeNull("the probe has to compile against the real Primitives assembly");

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        return compiler.CompileSource(source, "Probe.cs").Single();
    }

    /// <summary>
    /// THE CALL THE FENCE MUST LET THROUGH. <c>Add</c> is declared on the host-only
    /// <c>FlexNode</c> and inherited into <c>Row</c>, whose runtime twin carries it — so a component
    /// building a row child by child is naming <c>Row</c>, not the shape.
    /// </summary>
    [Fact]
    public void AnInheritedMethod_IsCallableThroughTheNodeThatInheritsIt()
    {
        var result = CompileBuild("""
            var row = new Row(gap: 8f);
            row.Add(Text("hi", TypeRole.BodyM));
            return row;
            """);

        result.Success.Should().BeTrue(
            "Add is inherited into a client-visible node, and the fence asks what the call went "
            + "THROUGH: " + string.Join(" | ", result.Errors.Select(e => e.Code + " " + e.Message)));
    }

    /// <summary>
    /// And the other direction, because a fence that stops reporting is not a narrower fence, it is
    /// a removed one: naming the SHAPE is still refused, which is why it carries the attribute — the
    /// runtime exports no <c>FlexNode</c>, so a component naming it emits an import of a missing
    /// export and dies at hydration.
    /// </summary>
    [Fact]
    public void TheSameCall_IsStillRefusedWhenTheComponentNamesTheShape()
    {
        var result = CompileBuild("""
            FlexNode row = new Row(gap: 8f);
            row.Add(Text("hi", TypeRole.BodyM));
            return row;
            """);

        result.Success.Should().BeFalse("the shape itself is what the runtime has no twin for");
        result.Errors.Should().Contain(e => e.Code == "EQ2010" && e.Message.Contains("FlexNode"),
            "the diagnostic names the type the component asked for");
    }
}
