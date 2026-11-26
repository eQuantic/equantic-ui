using eQuantic.Console;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Tests.Fixtures;

/// <summary>
/// A detector whose factory changes type at the same place (#1563): the first build hands it a factory of
/// <see cref="ArenaProbe"/>, the next one a factory of the dashboard's <see cref="HoldThenDrag"/>. Flutter's
/// <c>canUpdate</c> makes a new recognizer then, and so does the router, which compares the factories' types.
/// Run in C# by <c>GestureArenaFixtureTests</c> and transpiled for <c>gesture-arena.spec.ts</c>, where eqc marks each
/// construction with its type argument and the browser answers as C# does. An app's construction of the framework's
/// factory is marked too (<c>AGestureRecognizerOfTheAppsOwnTests</c>).
/// </summary>
public static class FactoryTypeChange
{
    /// <summary>What was heard while the place was built twice, and whether its recognizer was kept.</summary>
    public static List<string> Run()
    {
        var heard = new List<string>();
        var router = new GestureRouter();
        var first = router.Recognizers("place", [new GestureRecognizerFactoryWithHandlers<ArenaProbe>(
            () =>
            {
                heard.Add("probe.create");
                return new ArenaProbe("probe", heard);
            },
            _ => heard.Add("probe.initialize"))])[0];
        var second = router.Recognizers("place", [new GestureRecognizerFactoryWithHandlers<HoldThenDrag>(
            () =>
            {
                heard.Add("drag.create");
                return new HoldThenDrag();
            },
            drag => heard.Add(ReferenceEquals(drag, first) ? "drag.initialize the probe" : "drag.initialize its own"))])[0];
        heard.Add(ReferenceEquals(first, second) ? "kept" : "made new");
        return heard;
    }
}
