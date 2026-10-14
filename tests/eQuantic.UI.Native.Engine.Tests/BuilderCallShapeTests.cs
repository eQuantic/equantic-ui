using eQuantic.UI.Native.Generators;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A declaration on the builder reached through <c>?.</c> is read as one reached through <c>.</c>, in
/// every family the generator reads. The second shape is a member BINDING in the syntax tree, and a
/// family that looked only for a member access let it change what the app does at run time while the
/// build declared nothing: on a phone, a link that opens and a manifest that says nothing opens it.
/// </summary>
public class BuilderCallShapeTests
{
    [Fact]
    public void EveryFamily_ReadsACallThroughAConditionalAccess()
    {
        var program = CSharpSyntaxTree.ParseText("""
            using eQuantic.UI.Native.Hosting;

            public partial class Program
            {
                public static PhotonApplication CreateApp(string[] args)
                {
                    var builder = PhotonApplication.CreateBuilder(args);
                    builder.Bundle?.UrlScheme("acme");
                    builder.Capabilities?.UseCamera("To scan a card.");
                    builder.Entitlements?.RequireNetworkClient();
                    builder.Workspace?.OpensMail();
                    return builder.Build();
                }
            }
            """, path: "Program.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)TestReferences.Of(p))
            .Append(TestReferences.Of(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location))
            .Append(TestReferences.Of(typeof(eQuantic.UI.Native.Hosting.PhotonApplication).Assembly.Location));
        var compilation = CSharpCompilation.Create("App", [program], references,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication));

        var result = CSharpGeneratorDriver.Create(new PhotonProgramGenerator())
            .RunGenerators(compilation).GetRunResult().Results.Single();
        var generated = string.Join("\n", result.GeneratedSources.Select(source => source.SourceText.ToString()));

        result.Diagnostics.Should().BeEmpty();
        generated.Should().Contain("PhotonBundleKey(").And.Contain("\"acme\"");
        generated.Should().Contain("PhotonCapability(").And.Contain("To scan a card.");
        generated.Should().Contain("PhotonEntitlement(");
        generated.Should().Contain("PhotonOpenedScheme(\"mailto\")");
    }
}
