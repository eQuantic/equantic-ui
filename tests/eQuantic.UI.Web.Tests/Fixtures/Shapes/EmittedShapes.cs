using System;
using System.Collections.Generic;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Tests.Fixtures;

public static class EmittedShapes
{
    public static int SideOf(EmittedShape shape) => ((EmittedSquare)shape).Side;

    // A member the cast's type inherits, a property and a list (#1396).
    public static EmittedShape? Inside(EmittedShape shape) => ((EmittedCard)shape).Child;

    public static int Count(EmittedShape shape) => ((EmittedRow)shape).Children.Count;

    // A member declared on a base the cast's type sits under, read through a cast to that base.
    public static EmittedShape? Framed(EmittedShape shape) => ((EmittedFramed)shape).Child;

    // A node of the vocabulary over a base the server keeps: Pressable's Child is SingleChildNode's, and
    // Row's Children is FlexNode's, each read off the runtime's twin (#1396).
    public static VisualNode Pressed(VisualNode node) => ((Pressable)node).Child;

    public static int InARow(VisualNode node) => ((Row)node).Children.Count;
}
