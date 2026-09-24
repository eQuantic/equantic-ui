using System.Reflection;
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

        (new RenderContext().GetService<IClipboardish>()?.Read()).Should().Be("armed");
    }

    /// <summary>…and an absence is an absence rather than a throw, because that is what the TWIN
    /// answers: `getService(key): T | undefined`. A version here that threw would make the two sides
    /// disagree about a missing capability — SSR fails the request, the client renders on.</summary>
    [Fact]
    public void WithNothingArmed_TheCapabilityIsNull()
    {
        CapabilityScope.Current = null;

        new RenderContext().GetService<IClipboardish>().Should().BeNull();
    }

    /// <summary>
    /// ONE accessor, and the same shape as the vocabulary's — asserted by REFLECTION because the
    /// contract is with the transpiler rather than with a caller. `ServiceProviderStrategy`
    /// recognizes `GetService` and `GetRequiredService`; a `TryGetService` beside them fell through
    /// to an ordinary invocation and emitted `context.tryGetService(...)`, a method the runtime's
    /// `RenderContext` has never had. Found in review.
    /// </summary>
    [Fact]
    public void TheWebContextOffersTheSameCapabilityApiAsTheVocabulary()
    {
        var web = typeof(RenderContext).GetMethods()
            .Where(m => m.Name.Contains("Service", StringComparison.Ordinal))
            .Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

        web.Should().Equal(["GetService"],
            "the transpiler knows GetService and GetRequiredService by name; anything else here is "
            + "emitted as an ordinary call to a method the runtime does not have");

        // NULLABILITY, not the type: both return the method's own `T`, so comparing `ReturnType`
        // compares two open type parameters and passes for any signature at all. What separates a
        // `T?` from a `T` is the annotation, and that is the difference the twin cares about — it
        // answers `T | undefined`.
        var nullability = new NullabilityInfoContext();
        var webReturn = nullability.Create(
            typeof(RenderContext).GetMethod("GetService")!.ReturnParameter);
        var vocabularyReturn = nullability.Create(
            typeof(ComponentContext).GetMethod("GetService")!.ReturnParameter);

        webReturn.ReadState.Should().Be(NullabilityState.Nullable,
            "an absent capability is an answer here, because the runtime twin returns undefined "
            + "rather than throwing — a throw would make SSR fail a request the client renders");
        webReturn.ReadState.Should().Be(vocabularyReturn.ReadState,
            "the two contexts answer the same question, so they answer it in the same shape");
    }
}
