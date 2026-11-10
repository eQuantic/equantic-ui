using eQuantic.UI.Generators;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The app's <c>UseContentSecurityPolicy</c> call reaches its build as
/// <c>[assembly: ContentSecurityPolicy]</c> (#1269), the way the native capabilities reach theirs: the
/// app states the policy once, in <c>Program.cs</c>, and eqc reads the attribute to refuse the
/// escape hatch's inline handlers (EQ2115).
/// </summary>
public class ContentSecurityPolicyGeneratorTests
{
    private static (IReadOnlyList<string> Generated, Compilation Output) Run(string program)
    {
        var tree = CSharpSyntaxTree.ParseText(program, path: "Program.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)TestReferences.Of(path))
            .Append(TestReferences.Of(typeof(eQuantic.UI.Server.UIOptions).Assembly.Location))
            .Append(TestReferences.Of(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        var compilation = CSharpCompilation.Create("App", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new ContentSecurityPolicyGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        var generated = driver.GetRunResult().Results.Single().GeneratedSources
            .Select(source => source.SourceText.ToString()).ToList();
        return (generated, output);
    }

    [Fact]
    public void AnAppThatTurnsThePolicyOn_SaysSoInItsAssembly()
    {
        var (generated, output) = Run("""
            using eQuantic.UI.Server;

            public static class Startup
            {
                public static void Configure(UIOptions ui) =>
                    ui.UseContentSecurityPolicy(csp => csp.AllowConnectionsTo("https://api.example.com"));
            }
            """);

        generated.Should().ContainSingle().Which.Should().Contain("[assembly: global::eQuantic.UI.Server.ContentSecurityPolicy]");
        output.Assembly.GetAttributes().Select(attribute => attribute.AttributeClass?.ToDisplayString())
            .Should().Contain("eQuantic.UI.Server.ContentSecurityPolicyAttribute",
                "the attribute binds to the server's own type, which eqc reads by its name");
        output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
    }

    [Fact]
    public void AnAppThatDoesNot_SaysNothing()
    {
        var (generated, _) = Run("""
            using eQuantic.UI.Server;

            public static class Startup
            {
                public static void Configure(UIOptions ui) => ui.WithoutPoweredByHeader();

                // A method of the same name that is not the SDK's says nothing either.
                public static void UseContentSecurityPolicy() { }
                public static void Other() => UseContentSecurityPolicy();
            }
            """);

        generated.Should().BeEmpty();
    }
}
