using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A checkbox's label is read whole. It was clamped at two lines, so on a phone a consent lost its
/// end, the part that named the privacy policy it consents to, behind an ellipsis, and the box sat at
/// the middle of the lines it had rather than beside the first. The label now takes what the row
/// leaves beside the box and every line it needs, and the box is level with its first line.
/// </summary>
public class CheckboxLabelTests
{
    private const string Consent =
        "I agree that you keep these details to contact me about the service, as the privacy policy explains.";

    private const float ColumnWidth = 200;

    private static LayoutNode Realize(VisualNode root, Density density)
    {
        var builder = new DisplayListBuilder();
        return PhotonRealizer.Realize(root, 400, 400, PhotonTheme.Instance, ThemeMode.Light, ContrastLevel.Standard, builder,
            density: density).Root;
    }

    private static LayoutNode Find(LayoutNode node, Func<LayoutNode, bool> match)
    {
        if (match(node)) return node;
        foreach (var child in node.Children)
        {
            var found = Find(child, match);
            if (found is not null) return found;
        }
        return null!;
    }

    [Theory]
    [InlineData(Density.Comfortable, 22)]
    [InlineData(Density.Compact, 20)]
    public void ALongLabel_WrapsWhole_BesideTheBox_LevelWithItsFirstLine(Density density, float side)
    {
        var lineHeight = PhotonTheme.Instance.Type(TypeRole.BodyM).LineHeight;
        var root = Realize(new Column { new Box(new BoxStyle { Width = ColumnWidth }, new Checkbox(true, () => { }, Consent)) },
            density);

        var label = Find(root, n => n.Source is Text { Content: Consent });
        var box = Find(root, n => n.Source is Box { Style.CornerRadius.TopLeft: > 0 }
            && MathF.Abs(n.Bounds.Width - side) < 0.5f && MathF.Abs(n.Bounds.Height - side) < 0.5f);
        label.Should().NotBeNull();
        box.Should().NotBeNull();

        // Every line it needs: at 200dp this label takes more than the two it used to be cut at.
        label.Bounds.Height.Should().BeGreaterThan(2 * lineHeight + 0.5f, "a label is never cut short");
        // …inside the column, beside the box: a label that kept its one long line would leave it.
        label.Bounds.X.Should().BeGreaterThan(box.Bounds.X + box.Bounds.Width, "the label sits beside the box");
        (label.Bounds.X + label.Bounds.Width).Should().BeLessThanOrEqualTo(ColumnWidth + 0.5f, "the label wraps inside its row");
        // …and the box beside its first line, their centres level, not at the middle of the paragraph.
        (box.Bounds.Y + box.Bounds.Height / 2).Should().BeApproximately(label.Bounds.Y + lineHeight / 2, 0.5f,
            "the box is level with the label's first line");
    }

    [Fact]
    public void AOneLineLabel_KeepsTheBoxAtItsCentre()
    {
        var root = Realize(new Column { new Checkbox(false, () => { }, "News") }, Density.Comfortable);

        var label = Find(root, n => n.Source is Text { Content: "News" });
        var box = Find(root, n => n.Source is Box { Style.CornerRadius.TopLeft: > 0 }
            && MathF.Abs(n.Bounds.Width - 22) < 0.5f);
        (box.Bounds.Y + box.Bounds.Height / 2).Should().BeApproximately(label.Bounds.Y + label.Bounds.Height / 2, 0.5f,
            "a one-line label draws as it always did");
    }
}
