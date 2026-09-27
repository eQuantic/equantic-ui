namespace eQuantic.UI.Code;

/// <summary>What a line of a patch's hunk is (see <see cref="CodePatchLine"/>).</summary>
public enum CodePatchLineKind
{
    /// <summary>The same on both sides: a line of context.</summary>
    Context,

    /// <summary>Only in the original: removed.</summary>
    Removed,

    /// <summary>Only in the modified text: added.</summary>
    Added,
}
