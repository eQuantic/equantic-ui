using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The web's ambients ARE the vocabulary's ambients — one route, one capability resolver.
///
/// <para>
/// They used to be two. `Web/Dom/RouteData` and `Primitives/RouteValues` answered `Param` and
/// `Query` identically, each behind its own `AsyncLocal`, and `ServerRenderingService` armed both
/// on every request — building the second FROM the first, in a line whose comment said so. The Core
/// dissolution (#83) moved the write-once half down and left the web half where it was.
/// </para>
///
/// <para>
/// Two ambients holding the same thing is only ever as good as whoever remembers to arm both. This
/// asserts the shape that makes remembering unnecessary, by IDENTITY rather than by value: a
/// `RenderContext` that copied the route would satisfy an equality check and reintroduce exactly
/// the arrangement that was removed.
/// </para>
/// </summary>
public class OneAmbientAtTheWebSeamTests
{
    [Fact]
    public void TheWebsRoute_IsTheVocabularysRoute()
    {
        var armed = new RouteValues(
            new Dictionary<string, string> { ["slug"] = "getting-started" },
            new Dictionary<string, string> { ["page"] = "2" });
        RouteValues.Current = armed;
        try
        {
            new RenderContext().Route.Should().BeSameAs(armed,
                "the web seam reads the ambient route rather than a copy of it — a copy is how the "
                + "two drifted in the first place");
            new RenderContext().Route.Param("slug").Should().Be("getting-started");
            new RenderContext().Route.Query("page").Should().Be("2");
        }
        finally
        {
            RouteValues.ClearCurrent();
        }
    }

    /// <summary>The half an identity check forgets: with nothing armed, a page still gets a route
    /// it can ask questions of. `RenderContext.Route` was never nullable and must not become so.</summary>
    [Fact]
    public void WithNothingArmed_TheRouteIsEmptyRatherThanAbsent()
    {
        RouteValues.ClearCurrent();

        new RenderContext().Route.Should().BeSameAs(RouteValues.Empty);
        new RenderContext().Route.Param("slug").Should().BeNull();
    }

    private interface IClipboardish { string Read(); }

    private sealed class Clipboardish : IClipboardish
    {
        public string Read() => "armed";
    }

    /// <summary>
    /// The capability half. `RenderContext` carried its own `AsyncLocal` provider, a process-wide
    /// fallback and a per-instance dictionary; `ServerRenderingService` armed that AND
    /// `CapabilityScope` from the same container, every request. This asks the web context a
    /// question only the vocabulary's resolver was armed to answer.
    /// </summary>
    [Fact]
    public void TheWebsCapability_ComesFromTheVocabularysResolver()
    {
        using var _ = CapabilityScope.With<IClipboardish>(new Clipboardish());

        new RenderContext().TryGetService<IClipboardish>()?.Read().Should().Be("armed");
        new RenderContext().GetService<IClipboardish>().Read().Should().Be("armed");
    }

    /// <summary>…and an absence is still an absence, in the two shapes a caller asked for it.</summary>
    [Fact]
    public void WithNothingArmed_TheCapabilityIsNullOrSaysWhoNeededIt()
    {
        CapabilityScope.Current = null;

        new RenderContext().TryGetService<IClipboardish>().Should().BeNull();
        var thrown = Assert.Throws<InvalidOperationException>(
            () => new RenderContext().GetService<IClipboardish>());
        thrown.Message.Should().Contain("IClipboardish",
            "the message names the capability, because the reader is usually on the target that "
            + "does not have it");
    }
}
