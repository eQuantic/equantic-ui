using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Design-system fidelity goldens: the Button variant × state matrix (spec A12) rendered through the
/// engine with the real <see cref="PhotonTheme"/> tokens, in both modes. This is the design → engine
/// contract: a token or resolver change that shifts a pixel shows up here as a reviewable diff.
/// (Labels/icons join when the text stack lands — these are the container chrome.)
/// </summary>
public class GoldenButtonTests
{
    private static readonly Variant[] Variants =
    {
        Variant.Primary, Variant.Secondary, Variant.Destructive, Variant.Outline, Variant.Ghost,
        Variant.Link, Variant.Success, Variant.Warning, Variant.Info,
    };

    private static readonly InteractionState[] States =
    {
        InteractionState.Default, InteractionState.Pressed, InteractionState.Focused, InteractionState.Disabled,
    };

    private const float CellW = 120;
    private const float CellH = 40; // Medium
    private const float GapX = 14;
    private const float GapY = 12;
    private const float Margin = 16;

    private static void RenderMatrix(ThemeMode mode, string goldenName)
    {
        var theme = PhotonTheme.Instance;
        var width = (int)(Margin * 2 + States.Length * CellW + (States.Length - 1) * GapX);
        var height = (int)(Margin * 2 + Variants.Length * CellH + (Variants.Length - 1) * GapY);

        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(width, height);

        var builder = new DisplayListBuilder();
        builder.Clear(theme.Background.Resolve(mode));

        for (var row = 0; row < Variants.Length; row++)
        {
            for (var col = 0; col < States.Length; col++)
            {
                var style = ButtonStyles.Resolve(theme, mode, Variants[row], SizeVariant.Medium, States[col]);
                var bounds = new Rect(
                    Margin + col * (CellW + GapX),
                    Margin + row * (CellH + GapY),
                    CellW, CellH);
                ButtonRenderer.Draw(builder, bounds, style);
            }
        }

        backend.Render(builder.Build(), surface);
        GoldenImage.Match(surface, goldenName);
    }

    [Fact]
    public void ButtonMatrix_Light() => RenderMatrix(ThemeMode.Light, "button-matrix-light");

    [Fact]
    public void ButtonMatrix_Dark() => RenderMatrix(ThemeMode.Dark, "button-matrix-dark");
}
