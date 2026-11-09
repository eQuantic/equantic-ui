using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.ReferencedProjects;

/// <summary>
/// An app that turned on the Content-Security-Policy is refused, at build time, the escape hatch's
/// inline handlers (EQ2115, #1269).
/// <para>
/// <c>HtmlElement.OnClientClick</c> and <c>OnLoad</c> write JavaScript as text into an <c>onclick</c>
/// and an <c>onload</c>, and <c>DynamicElement.CustomAttributes</c> writes any attribute. A strict
/// policy refuses every inline handler in the browser, wherever the markup was written, so the only
/// place a developer heard of one was the console of a page that did nothing on a click. The build
/// reads every source it compiles, the app's and its referenced projects', since a library never
/// sees the <c>Program.cs</c> that turned the policy on.
/// </para>
/// <para>
/// The app says it turned the policy on through <c>[assembly: ContentSecurityPolicy]</c>, which the
/// SDK's generator writes from <c>UseContentSecurityPolicy</c>. The test writes the attribute and its
/// declaration itself: this host references no server, and the generator is held by its own test.
/// </para>
/// </summary>
public class InlineHandlerUnderAPolicyTests
{
    private const string Home =
        """
        using eQuantic.UI.Components;
        using eQuantic.UI.Primitives;

        namespace App.Screens;

        public sealed class Home : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context) =>
                Text("home", TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1);
        }
        """;

    /// <summary>What the SDK's generator writes, and the attribute's declaration the server ships.</summary>
    private const string Policy =
        """
        [assembly: eQuantic.UI.Server.ContentSecurityPolicy]

        namespace eQuantic.UI.Server
        {
            [System.AttributeUsage(System.AttributeTargets.Assembly)]
            public sealed class ContentSecurityPolicyAttribute : System.Attribute;
        }
        """;

    private const string Handlers =
        """
        using eQuantic.UI.Web;

        namespace NS;

        public static class Banners
        {
            private const string Load = "onload";

            public static DynamicElement Clicked() => new() { TagName = "button", OnClientClick = "go()" };

            public static DynamicElement Loaded() => new() { TagName = "img", OnLoad = "seen()" };

            public static DynamicElement Keyed() => new()
            {
                TagName = "div",
                CustomAttributes = new() { ["onmouseover"] = "hover()", ["data-kind"] = "banner" },
            };

            public static DynamicElement Paired() => new()
            {
                TagName = "div",
                CustomAttributes = new() { { Load, "x()" }, { "role", "note" } },
            };

            public static DynamicElement Indexed()
            {
                var element = new DynamicElement { TagName = "div", CustomAttributes = new() };
                element.CustomAttributes["onfocus"] = "focus()";
                element.CustomAttributes.Add("onblur", "blur()");
                element.CustomAttributes.Add("aria-label", "fine");
                return element;
            }
        }
        """;

    private static readonly string[] Refused =
        ["'OnClientClick'", "'OnLoad'", "\"onmouseover\"", "\"onload\"", "\"onfocus\"", "\"onblur\""];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryInlineHandler_IsRefused_InTheAppAndInWhatItReferences(bool inALibrary)
    {
        using var solution = inALibrary
            ? new Solution()
                .Library("Common", files: [("Banners.cs", Handlers.Replace("NS", "Common"))])
                .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories,
                    files: [("Screens/Home.cs", Home), ("Policy.cs", Policy)])
            : new Solution().App("App", globalUsings: SharedLibrary.Factories,
                files: [("Screens/Home.cs", Home), ("Policy.cs", Policy), ("Banners.cs", Handlers.Replace("NS", "App"))]);

        var built = solution.Build();

        built.Failed.Should().BeTrue("a strict policy refuses every one of these in the browser");
        var refusals = built.Errors.Split('\n').Where(line => line.Contains("error EQ2115")).ToList();
        refusals.Should().HaveCount(Refused.Length, "one refusal per handler, and none for a data or an aria attribute");
        foreach (var handler in Refused)
            refusals.Should().ContainSingle(line => line.Contains(handler), $"{handler} writes a handler as text");
        refusals.Should().OnlyContain(line => line.Contains("Banners.cs("), "each refusal points at the line that set it");
    }

    [Fact]
    public void AnAppThatTurnedNoPolicyOn_KeepsItsInlineHandlers()
    {
        using var solution = new Solution().App("App", globalUsings: SharedLibrary.Factories,
            files: [("Screens/Home.cs", Home), ("Banners.cs", Handlers.Replace("NS", "App"))]);

        var built = solution.Build();

        built.Errors.Should().NotContain("EQ2115", "the escape hatch is the app's, and no policy refuses its handlers");
    }
}
