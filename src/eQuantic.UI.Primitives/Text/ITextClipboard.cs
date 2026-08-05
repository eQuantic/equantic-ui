namespace eQuantic.UI.Primitives;

/// <summary>
/// The system clipboard, as far as a text field needs it.
/// <para>
/// Deliberately two methods and nothing else. The clipboard is a rich, typed, multi-flavour thing
/// on every platform, and none of that reaches a text field: it copies a string and pastes a
/// string. A wider surface here would be a wider surface to realize four times.
/// </para>
/// <para>
/// Null on a host means the platform has none — headless, a test, a shell that has not wired it —
/// and the copy keys then do nothing rather than pretending. Inside a text field the web needs no
/// realization (an <c>&lt;input&gt;</c> has done this forever); as a SERVICE a page asks for — a
/// "Copy C#" button — the web realizes it over <c>navigator.clipboard</c>.
/// </para>
/// </summary>
public interface ITextClipboard
{
    /// <summary>What is on the clipboard, or null when it holds nothing a field could paste.</summary>
    string? Read();

    void Write(string text);
}
