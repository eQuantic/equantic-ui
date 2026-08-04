namespace eQuantic.UI.Native.Framework;

/// <summary>
/// The system clipboard, as far as a text field needs it.
/// <para>
/// Deliberately two methods and nothing else. The clipboard is a rich, typed, multi-flavour thing
/// on every platform, and none of that reaches a text field: it copies a string and pastes a
/// string. A wider surface here would be a wider surface to realize four times.
/// </para>
/// <para>
/// Null on a host means the platform has none — headless, a test, a shell that has not wired it —
/// and the copy keys then do nothing rather than pretending. The web needs no realization at all:
/// an <c>&lt;input&gt;</c> has done this since before any of us.
/// </para>
/// </summary>
public interface ITextClipboard
{
    /// <summary>What is on the clipboard, or null when it holds nothing a field could paste.</summary>
    string? Read();

    void Write(string text);
}
