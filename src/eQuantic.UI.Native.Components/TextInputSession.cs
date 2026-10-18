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

    /// <summary>The id of what the keyboard was opened on, as of the last <see cref="Poll"/>.</summary>
    private int? _focus;

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
    /// <para>
    /// The caret counts only where the frame has something to type into. A field the app took away
    /// or disabled while it was being edited leaves the host's caret naming its place, and the
    /// keyboard goes down rather than starting over on nothing.
    /// </para>
    /// </summary>
    public TextInputChange Poll()
    {
        var editing = _host.EditingValue;
        var target = !_host.HasTextFocus ? TextInputTarget.None
            : editing is not null ? TextInputTarget.Text
            : TakesKeys() ? TextInputTarget.Keys
            : TextInputTarget.None;
        var focus = target == TextInputTarget.None ? null : _host.FocusedId;
        var obscure = target == TextInputTarget.Text && _host.TextTarget?.Obscure == true;
        var value = editing ?? TextEditingValue.Collapsed("", 0);

        var was = Target;
        var samePlace = target == was && focus == _focus && obscure == Obscure;
        Target = target;
        _focus = focus;
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
    /// has, whether the host took it or not, so a host that took it as it came reports nothing, and
    /// one that took it otherwise (a read-only or disabled field, a line break flattened, the app's
    /// own rewrite on the next frame) reports <see cref="TextInputChange.ValueChanged"/> at the next
    /// <see cref="Poll"/>. Answers false, and changes nothing, when the keyboard is not editing a
    /// field or the caret has left the one it was editing (a dictation that finished after another
    /// field was tapped): the next <see cref="Poll"/> starts the keyboard over on the new one.
    /// </summary>
    public bool Update(TextEditingValue value)
    {
        if (!OnItsField) return false;
        Value = value;
        return _host.UpdateEditingValue(value);
    }

    /// <summary>
    /// The keyboard's action key on a single-line field (Done): the field submits, as Enter does on a
    /// desktop, and then the edit ends and the keyboard goes down, which is what that key does on a
    /// phone (Flutter's <c>TextInputAction.done</c> unfocuses the same way). A submit that moved the
    /// caret to another field keeps it there. Answers false when the keyboard is not editing a field,
    /// or the caret has left the one it was editing.
    /// </summary>
    public bool Submit()
    {
        if (!OnItsField) return false;
        _host.KeyDown("Enter");
        if (_host.FocusedId == _focus) _host.EndEditing();
        return true;
    }

    /// <summary>
    /// Whether the caret is in a code surface or a sheet the frame still has. Its focus stop names
    /// it wherever it is, so one the rising keyboard clips for a frame is still being edited, as a
    /// field is (<see cref="PhotonHost.TextTarget"/>).
    /// </summary>
    private bool TakesKeys()
    {
        if (_host.CodeTarget is not null || _host.SheetTarget is not null) return true;
        if (_host.FocusedId is not { } focus || _host.LastFrame is not { } frame) return false;
        var stops = frame.FocusStops;
        for (var i = 0; i < stops.Count; i++)
            if (stops[i].Id == focus && stops[i] is { Code: not null } or { Sheet: not null }) return true;
        return false;
    }

    /// <summary>Whether the keyboard is editing a field and the caret is still in it: what the keyboard
    /// sends is for the field it was opened on, as of the last <see cref="Poll"/>.</summary>
    private bool OnItsField => Target == TextInputTarget.Text && _focus is not null && _host.FocusedId == _focus;
}
