using Android.Text;
using Android.Views;
using Android.Views.InputMethods;
using eQuantic.UI.Native.Components;
using Java.Lang;

namespace eQuantic.UI.Native.Shell.Android;

/// <summary>
/// The phone keyboard's way into what is being edited. Android's keyboard does not type keys into a
/// field: it reads the text around the caret, commits a word, marks the word it is suggesting for,
/// deletes around the caret and moves the selection, often several of these inside one batch.
/// <para>
/// A FIELD is edited as a document. The connection keeps a mirror of the field's editing value and
/// lets Android's own <see cref="BaseInputConnection"/> apply each edit to it exactly as it does to a
/// <c>TextView</c>'s text: the composition's spans, the selection, the batch. When the outermost
/// batch closes, the mirror's state goes to the <see cref="TextInputSession"/>, and the keyboard is
/// told the selection it ended with, which Android requires after every change. That is Flutter's
/// <c>InputConnectionAdaptor</c>, over the same <see cref="TextEditingValue"/>.
/// </para>
/// <para>
/// A CODE SURFACE or a SHEET keeps its text to itself, so the keyboard is told there is no text
/// (<c>TYPE_NULL</c>) and its edits arrive as what they are: committed text, a composition, a
/// deletion, keys. That is the desktop's protocol, and the same host methods take it.
/// </para>
/// <para>
/// A connection serves the field it was made for. Android closes the one it replaces later, and
/// closing finishes its composition: by then another field may hold the caret, so an edit from a
/// connection that is no longer the view's, or no longer on its field, goes nowhere.
/// </para>
/// </summary>
internal sealed class PhotonInputConnection : BaseInputConnection
{
    private readonly PhotonSurfaceView _view;
    private readonly PhotonHost _host;
    private readonly TextInputSession _session;
    private readonly InputMethodManager _keyboard;
    private readonly bool _document;
    private readonly string? _path;
    private readonly SpannableStringBuilder _mirror = new();
    private int _batch;
    private bool _edited;
    private bool _reportOwed;

    internal PhotonInputConnection(PhotonSurfaceView view, PhotonHost host, TextInputSession session,
        InputMethodManager keyboard)
        : base(view, session.Target == TextInputTarget.Text)
    {
        _view = view;
        _host = host;
        _session = session;
        _keyboard = keyboard;
        _document = session.Target == TextInputTarget.Text;
        _path = host.FocusedPath;
        if (_document) Show(session.Value);
        _reportOwed = false;
    }

    /// <summary>What every edit of the base class applies to: the mirror. Empty for a surface that
    /// takes keys, whose text the keyboard never sees.</summary>
    public override IEditable? Editable => _mirror;

    /// <summary>
    /// What the keyboard is told about the field before its first edit: a text keyboard for a field,
    /// with the field's selection and the text around it, and no keyboard text at all for a surface
    /// that takes keys. Neither runs full screen in landscape: the field stays where it is, above it.
    /// </summary>
    internal void Describe(EditorInfo info)
    {
        info.ImeOptions = ImeFlags.NoExtractUi | ImeFlags.NoFullscreen;
        if (!_document)
        {
            info.InputType = InputTypes.Null;
            return;
        }

        var multiline = _host.TextTarget?.Lines > 1;
        var type = InputTypes.ClassText;
        if (multiline) type |= InputTypes.TextFlagMultiLine;
        // A field that hides what is typed hides it from the keyboard too: no suggestions, nothing
        // shown in the strip above the keys, nothing learned.
        if (_session.Obscure)
        {
            type |= InputTypes.TextVariationPassword;
            info.ImeOptions |= ImeFlags.NoPersonalizedLearning;
        }
        info.InputType = type;
        // A single line ends on the keyboard's own key, which submits it; a multi-line field's Enter
        // is a line break.
        info.ImeOptions |= multiline ? ImeFlags.NoEnterAction : (ImeFlags)ImeAction.Done;
        info.InitialSelStart = _session.Value.SelectionBase;
        info.InitialSelEnd = _session.Value.SelectionExtent;
        if (!_session.Obscure && OperatingSystem.IsAndroidVersionAtLeast(30))
            info.SetInitialSurroundingText(_mirror);
    }

    // ---- The host changed the field ------------------------------------------------------------

    /// <summary>
    /// The field changed under the keyboard: the app rewrote it, a key edited it, the caret moved.
    /// The mirror takes the new state and the keyboard is told. A keyboard that was composing a word
    /// the change ended is started over instead, as Flutter does: keyboards do not expect the editor
    /// to end their composition, and one told only the new selection goes on suggesting for a word
    /// that is gone.
    /// </summary>
    internal void Apply(TextEditingValue value)
    {
        if (!_document) return;
        var composing = (GetComposingSpanStart(_mirror), GetComposingSpanEnd(_mirror));
        var ended = composing.Item1 >= 0 && composing != (value.ComposingStart, value.ComposingEnd);
        Show(value);
        if (ended)
        {
            _reportOwed = false;
            _keyboard.RestartInput(_view);
            return;
        }
        Settle();
    }

    /// <summary>
    /// A key the host just took (a key sent by the keyboard, or a keyboard attached to the phone)
    /// edited the field: the mirror takes it at once, because the keyboard's next edit reads the
    /// mirror and must not start from the text before the key.
    /// </summary>
    internal void Refresh()
    {
        if (!_document || !Serving || _host.EditingValue is not { } value || value == Current()) return;
        Show(value);
        Settle();
    }

    /// <summary>Puts a state into the mirror without sending it anywhere.</summary>
    private void Show(TextEditingValue value)
    {
        if (_mirror.ToString() != value.Text)
            _mirror.Replace(0, _mirror.Length(), new Java.Lang.String(value.Text));
        Selection.SetSelection(_mirror, value.SelectionBase, value.SelectionExtent);
        RemoveComposingSpans(_mirror);
        // The base class marks a composition with a span only it can name, so it marks this one.
        // Inside a batch of our own, so it does not count as the keyboard's edit.
        _batch++;
        try
        {
            if (value.HasComposing) base.SetComposingRegion(value.ComposingStart, value.ComposingEnd);
        }
        finally
        {
            _batch--;
        }
        _reportOwed = true;
    }

    // ---- The keyboard edits --------------------------------------------------------------------

    public override bool BeginBatchEdit()
    {
        _batch++;
        return true;
    }

    public override bool EndBatchEdit()
    {
        if (_batch > 0) _batch--;
        if (_batch == 0) Settle();
        return _batch > 0;
    }

    public override bool CommitText(ICharSequence? text, int newCursorPosition)
    {
        if (!_document)
        {
            var typed = text?.ToString() ?? "";
            return Serving && (typed == "\n" ? _host.KeyDown("Enter") : _host.CommitText(typed));
        }
        var done = base.CommitText(text, newCursorPosition);
        Edited();
        return done;
    }

    public override bool SetComposingText(ICharSequence? text, int newCursorPosition)
    {
        if (!_document) return Serving && _host.SetMarkedText(text?.ToString() ?? "");
        var done = base.SetComposingText(text, newCursorPosition);
        Edited();
        return done;
    }

    public override bool SetComposingRegion(int start, int end)
    {
        if (!_document) return false;
        var done = base.SetComposingRegion(start, end);
        Edited();
        return done;
    }

    public override bool FinishComposingText()
    {
        if (!_document)
            return !Serving || !_host.HasMarkedText || _host.CommitText(_host.MarkedText);
        var done = base.FinishComposingText();
        Edited();
        return done;
    }

    public override bool DeleteSurroundingText(int beforeLength, int afterLength)
    {
        if (!_document) return Serving && Delete(beforeLength, afterLength);
        var done = base.DeleteSurroundingText(beforeLength, afterLength);
        Edited();
        return done;
    }

    public override bool DeleteSurroundingTextInCodePoints(int beforeLength, int afterLength)
    {
        if (!_document) return Serving && Delete(beforeLength, afterLength);
        var done = base.DeleteSurroundingTextInCodePoints(beforeLength, afterLength);
        Edited();
        return done;
    }

    public override bool SetSelection(int start, int end)
    {
        if (!_document) return false;
        var done = base.SetSelection(start, end);
        Edited();
        return done;
    }

    /// <summary>The keyboard's action key (Done on a single line), which is the field's Enter: it
    /// submits.</summary>
    public override bool PerformEditorAction(ImeAction actionCode) => Serving && _host.KeyDown("Enter");

    /// <summary>A deletion around the caret of a surface that takes keys: that many Backspaces
    /// before it and Deletes after it, the keys its model already answers.</summary>
    private bool Delete(int before, int after)
    {
        for (var i = 0; i < before; i++) _host.KeyDown("Backspace");
        for (var i = 0; i < after; i++) _host.KeyDown("Delete");
        return true;
    }

    /// <summary>The keyboard changed the mirror: what it made goes to the host when its batch
    /// closes, or now if it opened none.</summary>
    private void Edited()
    {
        _edited = true;
        _reportOwed = true;
        if (_batch == 0) Settle();
    }

    /// <summary>
    /// Sends what is owed: the keyboard's edit to the host, then the selection to the keyboard.
    /// Android dedups a selection it was already told, so telling it after every change costs
    /// nothing when nothing moved.
    /// </summary>
    private void Settle()
    {
        if (_edited)
        {
            _edited = false;
            if (Serving && Current() is var value && value != _session.Value) _session.Update(value);
        }
        if (!_reportOwed) return;
        _reportOwed = false;
        if (!Serving) return;
        _keyboard.UpdateSelection(_view, Selection.GetSelectionStart(_mirror), Selection.GetSelectionEnd(_mirror),
            GetComposingSpanStart(_mirror), GetComposingSpanEnd(_mirror));
    }

    /// <summary>The mirror as an editing value: Android's selection start is the end that stays and
    /// its end the end that moves, which is a value's base and extent.</summary>
    private TextEditingValue Current() => new(_mirror.ToString(),
        Selection.GetSelectionStart(_mirror), Selection.GetSelectionEnd(_mirror),
        GetComposingSpanStart(_mirror), GetComposingSpanEnd(_mirror));

    /// <summary>Whether this connection still serves the edit it was made for.</summary>
    private bool Serving => ReferenceEquals(_view.Connection, this) && _host.FocusedPath == _path;
}
