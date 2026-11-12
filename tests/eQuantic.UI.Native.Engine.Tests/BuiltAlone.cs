using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A component built through an error boundary of its own, as a tree drawn once builds it: what a test
/// that only reads what one component builds needs (#1452). A test that reads what the boundary
/// reported or counted keeps its boundary and builds through it.
/// </summary>
internal static class BuiltAlone
{
    public static VisualNode BuildContainedAlone(this UiComponent component, ComponentContext context) =>
        new ComponentBoundary().BuildContained(component, context);
}
