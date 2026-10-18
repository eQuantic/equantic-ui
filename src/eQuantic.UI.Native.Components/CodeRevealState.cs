namespace eQuantic.UI.Native.Components;

/// <summary>
/// What a <see cref="Primitives.CodeSurface"/>'s position keeps for the host: the
/// <see cref="Primitives.ICodeSurfaceModel.RevealVersion"/> it was last brought into view at, so a
/// caret that moved since is brought into view and one that did not is left where the person put it.
/// </summary>
internal sealed class CodeRevealState
{
    /// <summary>Whether a frame has seen the surface yet: one seen for the first time is left where
    /// the layout put it.</summary>
    public bool Known;

    /// <summary>The version it was last seen at.</summary>
    public int Version;
}
