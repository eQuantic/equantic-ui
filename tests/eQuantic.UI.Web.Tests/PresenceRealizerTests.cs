using eQuantic.UI.Components;
using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Spec §06 — enter motion on the WEB realizer: <see cref="Presence"/> lowers to a mount-playing
/// animation class on the child's OWN root (no wrapper — flex/stack layout untouched, and the
/// reconciler's in-place patching never restarts it), with the mechanics in the generated
/// stylesheet: Base-duration keyframes, and Reduce Motion replacing movement with the spec's
/// short crossfade. The client lowering mirror is pinned in presence.spec.ts.
/// </summary>
public class PresenceRealizerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static List<string> Classes(HtmlNode node)
    {
        var found = new List<string>();
        void Walk(HtmlNode n)
        {
            if (n.Attributes.TryGetValue("class", out var cls) && cls != null) found.Add(cls);
            foreach (var child in n.Children) Walk(child);
        }
        Walk(node);
        return found;
    }

    [Fact]
    public void Stylesheet_CarriesTheEnterMechanics()
    {
        var css = PhotonCssGenerator.Generate(Theme);

        css.Should().Contain("@keyframes eq-presence-fade { from { opacity: 0; } }");
        css.Should().Contain("@keyframes eq-presence-slideup { from { opacity: 0; transform: translateY(16px); } }");
        css.Should().Contain(".eq-presence-fade { animation: eq-presence-fade var(--eq-motion-base) ease-out; }");
        // Reduce Motion: movement is REPLACED by the short crossfade (ReducedCrossfadeMs).
        css.Should().Contain(
            "@media (prefers-reduced-motion: reduce) { .eq-presence-fade, .eq-presence-slideup { animation: eq-presence-fade 120ms ease-out; } }");
    }

    [Fact]
    public void Presence_RidesTheClassOnTheChildRoot_NoWrapper()
    {
        var child = new Box(new BoxStyle { Width = 40, Height = 40, Background = Theme.Surface });
        var node = WebRealizer.Lower(new Presence(child, PresenceMotion.SlideUp), Theme).Render();

        // No wrapper: the class and the Box's own inline style live on the SAME element.
        node.Attributes["class"].Should().StartWith("eq-presence-slideup");
        node.Attributes.Should().ContainKey("style");
    }

    [Fact]
    public void TheOverlayTrio_EntersDeclaratively()
    {
        // Dialog: the whole layer fades as one.
        Classes(WebRealizer.Lower(new Dialog("T", "B", [new DialogAction("Ok")]), Theme).Render())
            .Should().Contain(c => c.Contains("eq-presence-fade"));

        // Toast: the pill rises (SlideUp).
        Classes(WebRealizer.Lower(new Toast("Saved", Variant.Success), Theme).Render())
            .Should().Contain(c => c.Contains("eq-presence-slideup"));

        // BottomSheet: scrim fades AND the sheet rises — two presences in one layer.
        var sheet = Classes(WebRealizer.Lower(new BottomSheet(new Text("Hi", TypeRole.BodyM)), Theme).Render());
        sheet.Should().Contain(c => c.Contains("eq-presence-fade"));
        sheet.Should().Contain(c => c.Contains("eq-presence-slideup"));
    }
}
