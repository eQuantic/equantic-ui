namespace eQuantic.UI.Native.Components;

/// <summary>
/// The conversation between a phone's keyboard and a host, kept in one place so the Android and iOS
/// shells only translate it: what the keyboard is editing, what it believes the field holds, and
/// what changed since it last heard. Flutter's <c>TextInputConnection</c>, platform-free.
/// <para>
/// A shell polls it after every frame and does what the answer says (open, start over, close, tell
/// the keyboard the new value), and hands it every edit the keyboard makes. It remembers the state the
/// keyboard HAS, so an edit is never echoed back to the keyboard that made it, and a change the app
/// made to the value always is.
/// </para>
/// </summary>
public sealed class TextInputSession
{
    private readonly PhotonHost _host;
    private string? _path;

    public TextInputSession(PhotonHost host) => _host = host;

    /// <summary>What the keyboard is editing, as of the last <see cref="Poll"/>.</summary>
    public TextInputTarget Target { get; private set; }

    /// <summary>
    /// The field's state as the keyboard has it: what it was last told, or what it last sent. Empty
    /// for <see cref="TextInputTarget.Keys"/>, whose text the keyboard never sees.
    /// </summary>
    public TextEditingValue Value { get; private set; } = TextEditingValue.Collapsed("", 0);

    /// <summary>The field hides what is typed: the keyboard must not suggest it, learn it or show it.</summary>
    public bool Obscure { get; private set; }

    /// <summary>
    /// What the keyboard has to do now. Reads the host after a frame: whether anything holds the
    /// caret, which field or surface, and whether the field's state is still the one the keyboard
    /// has. Each answer is given once; the keyboard is assumed to have done it.
    /// </summary>
    public TextInputChange Poll()
    {
        var editing = _host.EditingValue;
        var target = !_host.HasTextFocus ? TextInputTarget.None
            : editing is not null ? TextInputTarget.Text
            : TextInputTarget.Keys;
        var path = target == TextInputTarget.None ? null : _host.FocusedPath;
        var obscure = target == TextInputTarget.Text && _host.TextTarget?.Obscure == true;
        var value = editing ?? TextEditingValue.Collapsed("", 0);

        var was = Target;
        var samePlace = target == was && path == _path && obscure == Obscure;
        Target = target;
        _path = path;
        Obscure = obscure;

        if (target == TextInputTarget.None)
        {
            Value = value;
            return was == TextInputTarget.None ? TextInputChange.None : TextInputChange.Ended;
        }
        if (!samePlace)
        {
            Value = value;
            return was == TextInputTarget.None ? TextInputChange.Began : TextInputChange.Retargeted;
        }
        if (value == Value) return TextInputChange.None;
        Value = value;
        return TextInputChange.ValueChanged;
    }

    /// <summary>
    /// The keyboard's edit, as the field's whole new state. The host applies it
    /// (<see cref="PhotonHost.UpdateEditingValue"/>); this remembers it as the state the keyboard
    /// has, so a host that took it as it came reports nothing, and one that took it otherwise (a
    /// read-only field, a line break flattened, the app's own rewrite on the next frame) reports
    /// <see cref="TextInputChange.ValueChanged"/> at the next <see cref="Poll"/>. Answers false when
    /// the keyboard is not editing a field.
    /// </summary>
    public bool Update(TextEditingValue value)
    {
        if (Target != TextInputTarget.Text || !_host.UpdateEditingValue(value)) return false;
        Value = value;
        return true;
    }
}
