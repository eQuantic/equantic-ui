using System.Reflection;
using eQuantic.UI.Native.Generators;
using eQuantic.UI.Native.Hosting;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What <c>builder.Workspace.Opens…</c> declares, as the BUILD reads it. A phone answers "does
/// something open this link" only for the schemes its manifest lists, so the line that widens the
/// policy at run time also has to reach the manifest: the generator writes one
/// <see cref="PhotonOpenedSchemeAttribute"/> per scheme, and the build lists them for iOS and Android.
/// </summary>
public class OpenedSchemeDeclarationTests
{
    /// <summary>Where the test host has no phone, a stand-in type is what tells the generator it is
    /// building for one, as it does for the Android launcher.</summary>
    private const string AndroidStandIn = """
        namespace Android.App
        {
            public sealed class ActivityAttribute : System.Attribute { }
            public sealed class IntentFilterAttribute : System.Attribute { }
        }
        """;

    private const string IosStandIn = """
        namespace UIKit
        {
            public sealed class UIApplication { }
        }
        """;

    private static (IReadOnlyList<Diagnostic> Diagnostics, string? Declared) Run(string body, string? platform = null)
    {
        var program = $$"""
            using eQuantic.UI.Native.Hosting;
            using Microsoft.Extensions.DependencyInjection;

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
        var trees = new List<SyntaxTree> { CSharpSyntaxTree.ParseText(program, path: "Program.cs") };
        if (platform is not null) trees.Add(CSharpSyntaxTree.ParseText(platform, path: "Platform.cs"));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)TestReferences.Of(p))
            .Append(TestReferences.Of(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location))
            .Append(TestReferences.Of(typeof(PhotonApplication).Assembly.Location));
        var compilation = CSharpCompilation.Create("App", trees, references,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication));

        var result = CSharpGeneratorDriver.Create(new PhotonProgramGenerator())
            .RunGenerators(compilation)
            .GetRunResult().Results.Single();
        var declared = result.GeneratedSources
            .Where(source => source.HintName == "PhotonOpenedSchemes.g.cs")
            .Select(source => source.SourceText.ToString())
            .SingleOrDefault();
        return (result.Diagnostics, declared);
    }

    private static string[] Schemes(string? declared) => declared is null
        ? []
        : System.Text.RegularExpressions.Regex.Matches(declared, @"PhotonOpenedScheme\(""([^""]+)""\)")
            .Select(match => match.Groups[1].Value).ToArray();

    [Fact]
    public void EachSchemeTheAppOpens_IsDeclaredOnce_InOrdinalOrder()
    {
        var (diagnostics, declared) = Run("""
            builder.Workspace.OpensPhone().OpensMail();
            builder.Workspace.Opens("Slack").OpensMessages().OpensMail();
            """);

        diagnostics.Should().BeEmpty();
        Schemes(declared).Should().Equal("mailto", "slack", "sms", "tel");
    }

    [Fact]
    public void AnAppThatOpensNothingBeyondTheWeb_DeclaresNothing() =>
        Run("builder.Bundle.UrlScheme(\"acme\");").Declared.Should().BeNull(
            "the web is every app's, and an app always sees its own scheme");

    /// <summary>
    /// A name the builder refuses is nothing to write: the builder throws at that very line when the
    /// app starts, which is the report. Writing it would list a scheme no system can match.
    /// </summary>
    [Fact]
    public void ANameTheBuilderRefuses_IsNotDeclared()
    {
        var (diagnostics, declared) = Run("builder.Workspace.Opens(\"mailto:\").OpensPhone();", AndroidStandIn);

        diagnostics.Should().BeEmpty();
        Schemes(declared).Should().Equal("tel");
    }

    /// <summary>
    /// On a phone a scheme built at run time can never reach the manifest, and the app would open
    /// the link and still be told nothing opens it: an error at the line. A desktop reads the line at
    /// run time alone, so the same code is fine there.
    /// </summary>
    [Theory]
    [InlineData("android")]
    [InlineData("ios")]
    [InlineData("desktop")]
    public void ASchemeBuiltAtRunTime_IsAnErrorWhereAManifestIsWritten(string target)
    {
        var platform = target switch { "android" => AndroidStandIn, "ios" => IosStandIn, _ => null };

        var (diagnostics, declared) = Run("""
            var scheme = args.Length > 0 ? args[0] : "slack";
            builder.Workspace.Opens(scheme).OpensMail();
            """, platform);

        if (target == "desktop")
            diagnostics.Should().BeEmpty();
        else
            diagnostics.Should().ContainSingle().Which.Id.Should().Be("EQ3007");
        Schemes(declared).Should().Equal("mailto");
    }

    [Fact]
    public void AConstantIsReadAtBuildTime()
    {
        var (diagnostics, declared) = Run("""
            const string Chat = "slack";
            builder.Workspace.Opens(Chat);
            """, IosStandIn);

        diagnostics.Should().BeEmpty();
        Schemes(declared).Should().Equal("slack");
    }

    /// <summary>
    /// An app that registers its OWN policy decides without the builder, and the builder steps aside
    /// for it: what that policy widens by has to reach a phone's manifest as well, or the app opens a
    /// link that <c>CanOpen</c> then says nothing opens. Every form the policy takes is read, one
    /// scheme or several written at the call.
    /// </summary>
    [Fact]
    public void APolicyTheAppRegistersItself_IsDeclaredToo()
    {
        var (diagnostics, declared) = Run("""
            builder.Services.AddSingleton(eQuantic.UI.Primitives.OpenUrlPolicy.Web
                .Allowing("slack").Allowing(["Teams", "zoommtg"]).Allowing(new[] { "msteams" }));
            """, AndroidStandIn);

        diagnostics.Should().BeEmpty();
        Schemes(declared).Should().Equal("msteams", "slack", "teams", "zoommtg");
    }

    /// <summary>A sequence the build cannot see into, a variable or a spread, is as unreadable on a
    /// phone as a scheme built at run time.</summary>
    [Theory]
    [InlineData("android")]
    [InlineData("desktop")]
    public void APolicysSchemesBuiltAtRunTime_AreAnErrorWhereAManifestIsWritten(string target)
    {
        var (diagnostics, declared) = Run("""
            string[] more = args;
            builder.Services.AddSingleton(eQuantic.UI.Primitives.OpenUrlPolicy.Web
                .Allowing(more).Allowing(["slack", .. more]));
            """, target == "android" ? AndroidStandIn : null);

        diagnostics.Select(diagnostic => diagnostic.Id).Should().Equal(
            target == "android" ? ["EQ3007", "EQ3007"] : []);
        Schemes(declared).Should().Equal("slack");
    }

    /// <summary>
    /// .NET's own scheme names are <c>static readonly</c>, not <c>const</c>, and the builder's
    /// documentation offers them: the build reads what they hold. <c>file</c> passes as a NAME and
    /// is refused by the policy, so it is never listed.
    /// </summary>
    [Fact]
    public void DotNetsOwnSchemeNames_AreReadAtBuildTime()
    {
        var (diagnostics, declared) = Run("""
            builder.Workspace.Opens(System.Uri.UriSchemeMailto).Opens(System.Uri.UriSchemeHttps);
            """, IosStandIn);

        diagnostics.Should().BeEmpty();
        Schemes(declared).Should().Equal("https", "mailto");
        Run("builder.Workspace.Opens(System.Uri.UriSchemeFile);", IosStandIn).Declared
            .Should().BeNull("the builder refuses file at this line when the app starts");
    }

    /// <summary>
    /// The named doors open at run time exactly the scheme the build lists. DERIVED: every public
    /// <c>Opens…</c> method of the builder that takes nothing is called both ways, so a door added
    /// without its scheme in the shared rule fails here instead of opening a link a phone then says
    /// nothing opens.
    /// </summary>
    [Fact]
    public void EveryNamedDoor_ListsTheSchemeItOpens()
    {
        var doors = typeof(PhotonWorkspaceBuilder).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.Name.StartsWith("Opens", StringComparison.Ordinal)
                && method.GetParameters().Length == 0)
            .ToArray();
        doors.Should().NotBeEmpty();

        foreach (var door in doors)
        {
            var builder = PhotonApplication.CreateBuilder();
            door.Invoke(builder.Workspace, null);
            var opened = builder.Workspace.Declared.Schemes.Except(["http", "https"]).ToArray();

            Schemes(Run($"builder.Workspace.{door.Name}();", AndroidStandIn).Declared)
                .Should().Equal(opened, $"{door.Name}() opens {string.Join(", ", opened)} at run time");
        }
    }
}
