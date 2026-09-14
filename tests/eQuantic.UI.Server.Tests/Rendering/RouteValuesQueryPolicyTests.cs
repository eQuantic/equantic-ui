using eQuantic.UI.Primitives;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using eQuantic.UI.Server.Rendering;

namespace eQuantic.UI.Server.Tests.Rendering;

/// <summary>
/// A query key that appears TWICE means its first value, and both sides say so.
///
/// <para>
/// There were three answers to this one question and none of them was written down. The server
/// built its route with <c>StringValues.ToString()</c>, which comma-JOINS — <c>?tag=a&amp;tag=b</c>
/// arrived at a page as <c>"a,b"</c>, ASP.NET's own spelling and not a thing
/// <c>Query("tag")</c>'s caller can use. The client answered <c>"a"</c>, because
/// <c>URLSearchParams.get</c> does. And the collapse onto one route type nearly made it <c>"b"</c>,
/// because <c>Object.fromEntries</c> keeps the last — which is how this got noticed at all.
/// </para>
///
/// <para>
/// FIRST wins, because it is what the browser primitive already answered and the least surprising
/// reading of a URL: the first time a key appears is what it means. The twin holds the same policy
/// in <c>route-values.ts</c>, asserted in its own spec — this is the half that proves the SERVER
/// stopped joining.
/// </para>
/// </summary>
public class RouteValuesQueryPolicyTests
{
    private static RouteValues RouteFor(string queryString)
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString(queryString);
        return Build(context);
    }

    /// <summary>Reaches the service's own builder through the one public door that arms it, so this
    /// measures what a REQUEST produces rather than a copy of the logic.</summary>
    private static RouteValues Build(HttpContext context)
    {
        var method = typeof(ServerRenderingService).GetMethod("BuildRouteValues",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        method.Should().NotBeNull(
            "this test is about ServerRenderingService's own query policy — a reimplementation here "
            + "would agree with itself and prove nothing");
        return (RouteValues)method!.Invoke(null, [context])!;
    }

    [Fact]
    public void ARepeatedKey_MeansItsFirstValue()
    {
        RouteFor("?tag=a&tag=b").Query("tag").Should().Be("a",
            "the first time a key appears is what it means — and StringValues.ToString() would "
            + "hand the page \"a,b\", which is ASP.NET's spelling rather than an answer");
    }

    [Fact]
    public void ASingleKey_IsUnchanged()
    {
        RouteFor("?page=2").Query("page").Should().Be("2");
    }

    /// <summary>An empty repeated value is still a value, and still the first one. `?tag=&amp;tag=b`
    /// answers empty rather than falling through to the next.</summary>
    [Fact]
    public void AnEmptyFirstValue_IsStillTheAnswer()
    {
        RouteFor("?tag=&tag=b").Query("tag").Should().Be("");
    }

    /// <summary>
    /// The half the twin needed a guard for. A C# dictionary has no prototype, so this side was
    /// always right — but "always right by construction" is exactly the claim that stops being
    /// checked, and the client answered null here until it was fixed. The pair is the point.
    /// </summary>
    [Fact]
    public void AKeyNamedProto_IsAKeyLikeAnyOther()
    {
        RouteFor("?__proto__=x&tag=a").Query("__proto__").Should().Be("x");
        RouteFor("?__proto__=x&tag=a").Query("tag").Should().Be("a");
    }

    [Fact]
    public void AKeyThatIsNotThere_IsNull()
    {
        RouteFor("?page=2").Query("tag").Should().BeNull();
    }
}
