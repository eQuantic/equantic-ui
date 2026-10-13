using System.Text.RegularExpressions;
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
            .Select(p => (MetadataReference)TestReferences.Of(p))
            .Append(TestReferences.Of(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location))
            .Append(TestReferences.Of(
                typeof(eQuantic.UI.Native.Hosting.PhotonApplication).Assembly.Location));

        var compilation = CSharpCompilation.Create("App", [tree], references,
            new CSharpCompilationOptions(kind, nullableContextOptions: NullableContextOptions.Enable));

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

    /// <summary>
    /// The launcher activity an Android app gets, read as the text the generator writes: the test
    /// host has no Android, so a stand-in <c>ActivityAttribute</c> is what tells the generator it is
    /// building one. What the manifest then says is proved on the emulator, from the APK.
    /// </summary>
    private static string AndroidLauncher(string program = WellFormed)
    {
        var app = CSharpSyntaxTree.ParseText(program, path: "Program.cs");
        var android = CSharpSyntaxTree.ParseText(
            """
            namespace Android.App
            {
                public sealed class ActivityAttribute : System.Attribute { }
                public sealed class IntentFilterAttribute : System.Attribute { }
            }
            """,
            path: "Android.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)TestReferences.Of(p))
            .Append(TestReferences.Of(
                typeof(eQuantic.UI.Native.Hosting.PhotonApplication).Assembly.Location));
        var compilation = CSharpCompilation.Create("App", [app, android], references,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication));

        return CSharpGeneratorDriver.Create(new PhotonProgramGenerator())
            .RunGenerators(compilation)
            .GetRunResult().Results.Single().GeneratedSources
            .Single(source => source.HintName == "PhotonMainActivity.g.cs")
            .SourceText.ToString();
    }

    /// <summary>
    /// The activity keeps itself, and the app's state, through every change the host follows; and it
    /// declares predictive back, so the shell's back callback is the one Android 14 and later ask
    /// before the gesture ends (#771, #772).
    /// </summary>
    [Fact]
    public void TheAndroidLauncherFollowsItsOwnChangesAndDeclaresPredictiveBack()
    {
        var launcher = AndroidLauncher();

        launcher.Should().Contain("EnableOnBackInvokedCallback = true");
        foreach (var change in new[] { "Orientation", "ScreenSize", "SmallestScreenSize", "ScreenLayout",
                     "Keyboard", "KeyboardHidden", "Navigation", "UiMode", "Density" })
            Regex.IsMatch(launcher, $@"ConfigChanges\.{change}\b").Should().BeTrue(
                $"a {change} change is the host's to follow, not a reason to rebuild the activity");
    }

    private const string WithSchemes = """
        using eQuantic.UI.Native.Hosting;

        public partial class Program
        {
            public static PhotonApplication CreateApp(string[] args)
            {
                var builder = PhotonApplication.CreateBuilder(args);
                builder.Bundle.UrlScheme("acme");
                builder.Bundle.UrlScheme("Acme-Beta");
                builder.Bundle.UrlScheme("acme");
                return builder.Build();
            }
        }
        """;

    /// <summary>
    /// The scheme an app declares once on the builder is the filter Android matches a link against,
    /// on the launcher the generator writes: until #777 it reached the Mac's bundle and nothing on
    /// a phone, so a link opened nothing there.
    /// </summary>
    [Fact]
    public void TheAndroidLauncherIsOpenedByTheSchemesTheAppDeclared()
    {
        var launcher = AndroidLauncher(WithSchemes);

        launcher.Should().Contain("[global::Android.App.IntentFilter(");
        launcher.Should().Contain("global::Android.Content.Intent.ActionView");
        launcher.Should().Contain("global::Android.Content.Intent.CategoryDefault");
        launcher.Should().Contain("global::Android.Content.Intent.CategoryBrowsable",
            "a link in a browser reaches only an activity that says it may");
        launcher.Should().Contain("DataSchemes = new[] { \"acme\", \"acme-beta\" }",
            "each scheme once, lower-cased, in the order the app declared them");
    }

    /// <summary>
    /// The filter is the SYSTEM's, and an intent aimed at the exported launcher by name is not held
    /// against it. The shell holds it against the same schemes, which the launcher carries as code.
    /// </summary>
    [Fact]
    public void TheAndroidLauncherKnowsTheSchemesItAnswers()
    {
        var launcher = AndroidLauncher(WithSchemes);

        launcher.Should().Contain("IReadOnlyList<string> UrlSchemes { get; } =");
        Regex.IsMatch(launcher, @"UrlSchemes \{ get; \} =\s+new\[\] \{ ""acme"", ""acme-beta"" \};").Should().BeTrue();
        AndroidLauncher().Should().NotContain("UrlSchemes", "an app that declares none overrides nothing");
    }

    [Fact]
    public void AnAppThatDeclaresNoScheme_GetsNoFilterForLinks() =>
        AndroidLauncher().Should().NotContain("IntentFilter");

    /// <summary>
    /// One activity in one task for an app that links open: a second instance is a second surface
    /// and a second tree over the first one's state. An app that declares no scheme keeps Android's
    /// own launch mode, since a single task re-entered from its icon ends what it had opened on top
    /// of itself, a photo picker among them.
    /// </summary>
    [Fact]
    public void TheAndroidLauncherIsASingleTask_OnlyWhenLinksOpenIt()
    {
        AndroidLauncher(WithSchemes).Should().Contain("LaunchMode = global::Android.Content.PM.LaunchMode.SingleTask");
        AndroidLauncher().Should().NotContain("LaunchMode");
    }
}
