using eQuantic.UI.Native.Generators;
using eQuantic.UI.Native.Hosting;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A capability is declared only when an API uses it (#779). <c>Microphone</c>, <c>PhotoLibraryAdd</c>,
/// <c>Contacts</c> and <c>Notifications</c> wrote a permission no contract used, and App Review and
/// Google Play both reject an app for that. The four builder methods went, and the general form is refused
/// by the generator, naming the capability, where the manifest would have been written.
/// </summary>
public class ACapabilityWithNoApiTests
{
    private static IReadOnlyList<Diagnostic> Run(string body)
    {
        var program = $$"""
            using eQuantic.UI.Native.Hosting;
            using eQuantic.UI.Primitives;

            public partial class Program
            {
                public static PhotonApplication CreateApp(string[] args)
                {
                    var builder = PhotonApplication.CreateBuilder(args);
                    {{body}}
                    return builder.Build();
                }
            }
            """;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)TestReferences.Of(p))
            .Append(TestReferences.Of(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location))
            .Append(TestReferences.Of(typeof(PhotonApplication).Assembly.Location));
        var compilation = CSharpCompilation.Create("App", [CSharpSyntaxTree.ParseText(program, path: "Program.cs")], references,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication));

        return CSharpGeneratorDriver.Create(new PhotonProgramGenerator())
            .RunGenerators(compilation).GetRunResult().Results.Single().Diagnostics;
    }

    [Theory]
    [InlineData("Microphone")]
    [InlineData("PhotoLibraryAdd")]
    [InlineData("Contacts")]
    [InlineData("Notifications")]
    public void TheGeneralForm_IsRefused_NamingTheCapability(string capability)
    {
        var diagnostics = Run($"builder.Capabilities.Use(DeviceCapability.{capability}, \"Why it is needed.\");");

        var refused = diagnostics.Should().ContainSingle().Which;
        refused.Id.Should().Be("EQ3008");
        refused.Severity.Should().Be(DiagnosticSeverity.Error);
        refused.GetMessage().Should().Contain(capability);
    }

    [Theory]
    [InlineData("builder.Capabilities.UseCamera(\"Reads the code on your card.\");")]
    [InlineData("builder.Capabilities.Use(DeviceCapability.Location, \"Finds the nearest branch.\");")]
    public void ACapabilityAnApiUses_Builds(string body) => Run(body).Should().BeEmpty();

    [Theory]
    [InlineData("UseMicrophone")]
    [InlineData("UsePhotoLibraryAdd")]
    [InlineData("UseContacts")]
    [InlineData("UseNotifications")]
    public void TheNamedMethods_AreGone(string method) =>
        typeof(PhotonCapabilitiesBuilder).GetMethod(method).Should().BeNull("a method that invites a rejected app has no place on the builder");
}
