using System;
using System.Collections.Generic;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Tests.Fixtures;

public static class EmittedConfigs
{
    public static BoxStyle Inked() => new BoxStyle { Foreground = new ColorToken(Color.Black) };

    public static VisualNode Grouped(VisualNode child) =>
        new Group(child) { Label = "Totals", Bookmark = "totals", AlignSelf = CrossAlign.Center, GridSpan = 2 };

    public static Spacer Rigid() => new Spacer { FixedLength = 8 };

    // Members the constructor takes as parameters too, which an initializer sets through the config.
    public static Image Labelled() => new Image("a.png", 1, 2) { Label = "Chart" };

    public static Text Titled() => new Text("Totals") { Role = TypeRole.Title, MaxLines = 2 };

    public static Group Reparented(VisualNode first, VisualNode second) => new Group(first) { Child = second };

    public static SafeArea Inset(VisualNode child) => new SafeArea(child) { Edges = 3 };

    public static LinearGradient Downward() =>
        new LinearGradient(new ColorToken(Color.Black), new ColorToken(Color.White)) { Direction = GradientDirection.ToBottom };
}
