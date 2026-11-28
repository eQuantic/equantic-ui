using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// What a <see cref="OpacityGradient"/> writes as the CSS gradient <c>mask-image</c> takes (#1938): its
/// <see cref="OpacityGradient.Resolved"/> stops as opacities of black, since a mask reads only alpha. The web writes the
/// resolved list and never the stops as given, so the fix-up CSS would do has nothing left to do and the two
/// targets cannot follow two readings of one list. A single stop is written twice, at 0% and at 100%, a form
/// every engine accepts. The numbers go through <see cref="TokenCss.Number"/>, the four-decimal writer the
/// runtime's <c>num</c> twins, so the server and the browser mint the same string byte for byte.
/// <para>
/// The shape keyword is left out of a radial gradient: two lengths, and any size keyword alone, mean an
/// ellipse, which is the only shape a <see cref="RadialOpacityGradient"/> has.
/// </para>
/// </summary>
internal sealed class OpacityGradientCss : IOpacityGradientVisitor<Nothing, string>
{
    /// <summary>The visitor holds no state, so one serves every mask.</summary>
    internal static readonly OpacityGradientCss Instance = new();

    public string Visit(LinearOpacityGradient gradient, Nothing state) =>
        $"linear-gradient({TokenCss.Number(gradient.Angle)}deg, {Stops(gradient)})";

    public string Visit(RadialOpacityGradient gradient, Nothing state) =>
        $"radial-gradient({Extent(gradient.Extent)} at {Percent(gradient.Center.X)} {Percent(gradient.Center.Y)}, {Stops(gradient)})";

    private static string Extent(RadialExtent extent) => extent.Kind switch
    {
        RadialExtentKind.Fraction => $"{Percent(extent.X)} {Percent(extent.Y)}",
        RadialExtentKind.Dp => $"{TokenCss.Px(extent.X)} {TokenCss.Px(extent.Y)}",
        RadialExtentKind.ClosestSide => "closest-side",
        RadialExtentKind.FarthestSide => "farthest-side",
        RadialExtentKind.ClosestCorner => "closest-corner",
        _ => "farthest-corner",
    };

    private static string Stops(OpacityGradient gradient) => gradient.Resolved is [var only]
        ? $"{Stop(only.Opacity, "0%")}, {Stop(only.Opacity, "100%")}"
        : string.Join(", ", gradient.Resolved.Select(stop => Stop(stop.Opacity, Percent(stop.Offset))));

    private static string Stop(float opacity, string position) => $"rgb(0 0 0 / {TokenCss.Number(opacity)}) {position}";

    /// <summary>A fraction as a CSS percentage, to the four decimals the runtime's twin writes.</summary>
    private static string Percent(float fraction) => $"{TokenCss.Number(fraction * 100)}%";
}
