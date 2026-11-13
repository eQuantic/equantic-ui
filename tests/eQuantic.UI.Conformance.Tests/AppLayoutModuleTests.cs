using eQuantic.UI.Compiler.Tests.ReferencedProjects;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The app's layout is an ENTRY POINT of the bundle, as a page is (#1129): the browser loads it by the
/// name the server writes into the document (<c>/_equantic/ConsoleLayout.js</c>), so a layout that was
/// only transpiled, or bundled into a page's chunk, is a document that cannot show its frame.
/// <para>
/// The build reads which type is the layout from the line the server reads it from, the app's
/// <c>UseLayout&lt;T&gt;()</c>, bound against the SDK's real options type (this project references the
/// server). The layout's file sits in a folder that is neither the project's root nor <c>Pages</c>, so
/// the positional rule makes nothing of it, and the control without the call proves it.
/// </para>
/// </summary>
public class AppLayoutModuleTests
{
    private const string Home =
        """
        using eQuantic.UI.Primitives;

        namespace App.Pages;

        [Page("/")]
        public sealed class Home : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context) => new Text("home", TypeRole.BodyM);
        }
        """;

    private const string Layout =
        """
        using eQuantic.UI.Primitives;

        namespace {0};

        public sealed class ConsoleLayout(VisualNode body) : StatefulComponent
        {
            private bool _open;

            public override VisualNode Build(ComponentContext context)
            {
                var column = new Column(gap: Space.S2);
                column.Add(new Text(_open ? "open" : "closed", TypeRole.BodyM));
                column.Add(body);
                return column;
            }
        }
        """;

    private static string Program(string layout) =>
        $$"""
        var options = new eQuantic.UI.Server.UIOptions();
        {{layout}}
        """;

    private static string LayoutIn(string space) => Layout.Replace("{0}", space);

    [SkippableFact]
    public void TheLayoutTheAppDeclares_IsBundledAsAnEntryPoint()
    {
        var bun = JsExecutor.RequireBun();
        using var solution = new Solution().App("App", files:
        [
            ("Program.cs", Program("options.UseLayout<App.Shell.ConsoleLayout>();")),
            ("Pages/Home.cs", Home),
            ("Shell/ConsoleLayout.cs", LayoutIn("App.Shell")),
        ]);

        var built = solution.Build(bun: bun);

        built.Failed.Should().BeFalse($"the app builds and bundles:\n{built.Errors}");
        var module = Path.Combine(built.OutputDirectory, "ConsoleLayout.js");
        File.Exists(module).Should().BeTrue(
            $"the browser imports the layout by name, as it imports a page; the bundle wrote: {string.Join(", ", Written(built))}");
        File.ReadAllText(module).Should().MatchRegex(@"export\s*\{[^}]*\bConsoleLayout\b",
            "the boot reads the class off the module by the layout's name");
    }

    [SkippableFact]
    public void WithoutTheCall_TheSameTypeIsNoEntryPoint()
    {
        // The control: the same layout in the same folder, which the app never declares. Nothing imports
        // it, so the bundle has no reason to write it, and the test above is the call's doing.
        var bun = JsExecutor.RequireBun();
        using var solution = new Solution().App("App", files:
        [
            ("Program.cs", Program("_ = options;")),
            ("Pages/Home.cs", Home),
            ("Shell/ConsoleLayout.cs", LayoutIn("App.Shell")),
        ]);

        var built = solution.Build(bun: bun);

        built.Failed.Should().BeFalse($"the app builds and bundles:\n{built.Errors}");
        File.Exists(Path.Combine(built.OutputDirectory, "Home.js")).Should().BeTrue("the page is bundled either way");
        File.Exists(Path.Combine(built.OutputDirectory, "ConsoleLayout.js")).Should().BeFalse();
    }

    [SkippableFact]
    public void ALayoutOfAReferencedProject_IsCompiledAndBundled_ThoughNothingImportsIt()
    {
        // A referenced project's type is a module only when the build reaches it (#624), and no module
        // imports a layout: the call that names it is what reaches it.
        var bun = JsExecutor.RequireBun();
        using var solution = new Solution()
            .Library("Frame", files: [("ConsoleLayout.cs", LayoutIn("Frame"))])
            .App("App", references: ["Frame"], files:
            [
                ("Program.cs", Program("options.UseLayout<Frame.ConsoleLayout>();")),
                ("Pages/Home.cs", Home),
            ]);

        var built = solution.Build(bun: bun);

        built.Failed.Should().BeFalse($"the app builds and bundles:\n{built.Errors}");
        File.Exists(Path.Combine(built.OutputDirectory, "ConsoleLayout.js")).Should().BeTrue(
            $"the bundle wrote: {string.Join(", ", Written(built))}");
    }

    private static IEnumerable<string> Written(Built built) =>
        Directory.Exists(built.OutputDirectory)
            ? Directory.GetFiles(built.OutputDirectory, "*.js").Select(file => Path.GetFileName(file))
            : [];
}
