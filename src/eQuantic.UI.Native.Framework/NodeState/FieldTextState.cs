namespace eQuantic.UI.Native.Framework;

/// <summary>
/// What a <see cref="Primitives.TextEntry"/>'s position keeps: the text the field shows, and the node
/// it last took a value from. Read and written through <see cref="FieldText"/>, never directly, so
/// every reader of a field asks the same question the same way.
/// </summary>
internal sealed class FieldTextState
{
    /// <summary>What the field shows: what was typed, or the value the page last built it with.</summary>
    public string Text = "";

    /// <summary>The node the field last took its text from. Another one means the page built the field
    /// again, and the field shows what that one holds.</summary>
    public Primitives.TextEntry? From;
}
