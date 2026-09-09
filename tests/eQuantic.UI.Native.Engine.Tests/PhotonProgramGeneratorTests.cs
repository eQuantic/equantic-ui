using eQuantic.UI.Native.Generators;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The generator that writes the half of <c>Program</c> that is ceremony. It had three diagnostics
/// and no tests: the two it shipped with were never exercised, and the third exists because a real
/// app adopting the SDK could not get past its first build.
/// <para>
/// That app (eQuantic Code, migrating from a hand-written csproj) hit <c>CS0017</c> — "more than one
/// entry point" — pointing at its own <c>Main</c> and naming a generated file it had never heard of.
/// The compiler was right and the message was useless: nothing in it says the SDK writes the entry
/// point for you. The session resolved it by reading <c>samples/PhotonDesktop/Program.cs</c> and
/// noticing what was NOT there, which is not a migration path.
/// </para>
/// </summary>
public class PhotonProgramGeneratorTests
{
    private static IReadOnlyList<Diagnostic> Run(string appCode, OutputKind kind = OutputKind.ConsoleApplication)
    {
        var tree = CSharpSyntaxTree.ParseText(appCode, path: "Program.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location))
            .Append(MetadataReference.CreateFromFile(
                typeof(eQuantic.UI.Native.Hosting.PhotonApplication).Assembly.Location));

        var compilation = CSharpCompilation.Create("App", [tree], references,
            new CSharpCompilationOptions(kind, nullableContextOptions: NullableContextOptions.Enable));

        CSharpGeneratorDriver.Create(new PhotonProgramGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out _, out _)
            .GetRunResult();

        return CSharpGeneratorDriver.Create(new PhotonProgramGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out _, out _)
            .GetRunResult().Results.Single().Diagnostics;
    }

    /// <summary>The shape the samples use, and the one a migration arrives at: CreateApp, no Main.</summary>
    private const string WellFormed = """
        using eQuantic.UI.Native.Hosting;

        public partial class Program
        {
            public static PhotonApplication CreateApp(string[] args) =>
                PhotonApplication.CreateBuilder(args).Build();
        }
        """;

    [Fact]
    public void AProgramWithNoEntryPointOfItsOwn_IsTheShapeTheGeneratorWants()
    {
        Run(WellFormed).Should().BeEmpty("the generated half supplies the Main");
    }

    /// <summary>
    /// The migration case. The app keeps the Main it always had, and the error it gets must name
    /// that Main, say the SDK writes one, and say where the old body goes — because "delete your
    /// Main" is only safe advice next to "put what it did in CreateApp".
    /// </summary>
    [Fact]
    public void AnAppThatKeptItsOwnMain_IsToldWhyAndWhatToDoWithIt()
    {
        var diagnostics = Run("""
            using eQuantic.UI.Native.Hosting;

            public partial class Program
            {
                public static PhotonApplication CreateApp(string[] args) =>
                    PhotonApplication.CreateBuilder(args).Build();

                public static void Main(string[] args) => CreateApp(args).Run();
            }
            """);

        var reported = diagnostics.Should().ContainSingle().Subject;
        reported.Id.Should().Be("EQ3006");
        reported.Severity.Should().Be(DiagnosticSeverity.Error);
        reported.Location.GetLineSpan().StartLinePosition.Line.Should().Be(7, "it points at the app's Main");
        var message = reported.GetMessage();
        message.Should().Contain("CreateApp", "the fix is named, not implied");
        message.Should().Contain("Android", "why the work goes INTO CreateApp rather than a Main");
    }

    /// <summary>
    /// Top-level statements synthesise a Main, so `dotnet new console` plus a CreateApp is the same
    /// collision wearing different syntax — and the one an app is most likely to arrive in.
    /// </summary>
    [Fact]
    public void TopLevelStatements_AreTheSameCollision()
    {
        var diagnostics = Run("""
            using eQuantic.UI.Native.Hosting;

            System.Console.WriteLine("starting");

            public partial class Program
            {
                public static PhotonApplication CreateApp(string[] args) =>
                    PhotonApplication.CreateBuilder(args).Build();
            }
            """);

        diagnostics.Should().ContainSingle().Which.Id.Should().Be("EQ3006");
    }

    /// <summary>
    /// A LIBRARY has no entry point to clash with and must stay silent, or every project in a
    /// consumer's solution that merely references the hosting assembly fails its build.
    /// </summary>
    [Fact]
    public void ALibrary_IsNotAnAppMissingItsProgram()
    {
        Run("""
            using eQuantic.UI.Native.Hosting;

            public static class Helpers
            {
                public static string Describe(PhotonApplication app) => app.ToString() ?? "";
            }
            """, OutputKind.DynamicallyLinkedLibrary).Should().BeEmpty();
    }

    /// <summary>EQ3001 and EQ3002 shipped without a test between them. They have one each now.</summary>
    [Fact]
    public void AnExecutableWithNoCreateApp_SaysSo()
    {
        var diagnostics = Run("""
            using eQuantic.UI.Native.Hosting;

            public static class NotAProgram
            {
                public static PhotonApplication Build(string[] args) =>
                    PhotonApplication.CreateBuilder(args).Build();
            }
            """);

        diagnostics.Should().ContainSingle().Which.Id.Should().Be("EQ3001");
    }

    [Fact]
    public void TwoCreateApps_AreTwoPrograms()
    {
        var diagnostics = Run("""
            using eQuantic.UI.Native.Hosting;

            public partial class Program
            {
                public static PhotonApplication CreateApp(string[] args) =>
                    PhotonApplication.CreateBuilder(args).Build();
            }

            public static class Other
            {
                public static PhotonApplication CreateApp(string[] args) =>
                    PhotonApplication.CreateBuilder(args).Build();
            }
            """);

        diagnostics.Should().ContainSingle().Which.Id.Should().Be("EQ3002");
    }
}
