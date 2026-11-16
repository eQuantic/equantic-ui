using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>What a person asks the menu of (#1531): registered where it was drawn, for the host to answer by.</summary>
internal sealed partial class EmitVisitor
{
    private void EmitContextTarget(ContextTarget node, EmitState s)
    {
        if (s.Node.Element is { } at) s.Input.Add(new ContextRegion(s.Node.Bounds, s.Node.Bounds, at, node));
        Descend(s);
    }
}
