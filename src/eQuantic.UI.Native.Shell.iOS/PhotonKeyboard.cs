using CoreGraphics;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Primitives;
using Foundation;
using UIKit;

namespace eQuantic.UI.Native.Shell.iOS;

/// <summary>
/// The phone's keyboard on iOS. It opens when the host's edit begins, starts over when the edit
/// moves, closes when it ends, and keeps the proxy it types into (<see cref="PhotonTextProxy"/>) and
/// the host's field in step through a <see cref="TextInputSession"/>, read after each frame.
/// <para>
/// A FIELD is a document: the proxy holds its editing value, UIKit edits it (a word autocorrected,
/// a suggestion picked, dictation, a composition, which UIKit keeps IN the text as the host does),
/// and every change goes to the host as the field's whole new state. A change the host makes goes
/// back into the proxy. A CODE SURFACE or a SHEET keeps its text to itself: the proxy stays empty,
/// and what is typed reaches the host as text and keys, as on a desktop.
/// </para>
/// <para>
/// The keyboard's frame becomes the host's <see cref="PhotonHost.ViewInsets"/>, so the tree lays
/// out above it and the field being edited is scrolled into what is left.
/// </para>
/// </summary>
internal sealed class PhotonKeyboard : IDisposable
{
    private readonly UIView _view;
    private readonly PhotonHost _host;
    private readonly TextInputSession _session;
    private readonly PhotonTextProxy _proxy = new();
    private readonly NSObject _moved;
    private readonly NSObject _hidden;
    private bool _applying;
    private bool _resigning;

    internal PhotonKeyboard(UIView view, PhotonHost host)
    {
        _view = view;
        _host = host;
        _session = new TextInputSession(host);
        _proxy.Changed += (_, _) => Edited();
        _proxy.SelectionChanged += (_, _) => Edited();
        // The person put the keyboard away (an iPad's hide key): the edit ends with it.
        _proxy.Ended += (_, _) =>
        {
            if (!_resigning) _host.EndEditing();
        };
        _proxy.ShouldChangeText = ShouldChange;
        _proxy.BackspaceTaken = Backspace;
        _proxy.KeyTaken = Key;
        view.AddSubview(_proxy);
        _moved = UIKeyboard.Notifications.ObserveWillChangeFrame((_, e) => Covered(e.FrameEnd));
        _hidden = UIKeyboard.Notifications.ObserveWillHide((_, _) => _host.ViewInsets = default);
    }

    /// <summary>Does what the session says after a frame: open, start over, close, or take the
    /// field's new state into the proxy. Then the proxy follows the caret.</summary>
    internal void Sync()
    {
        var change = _session.Poll();
        switch (change)
        {
            case TextInputChange.Began or TextInputChange.Retargeted:
                Configure();
                break;
            case TextInputChange.Ended:
                Resign();
                // Emptied, so the last field's text (a password, say) does not stay behind in it.
                Show(TextEditingValue.Collapsed("", 0));
                break;
            case TextInputChange.ValueChanged when _session.Target == TextInputTarget.Text:
                Show(_session.Value);
                break;
        }
        if (_session.Target == TextInputTarget.None) return;
        Place();
        // Taken whenever the window can give it: the first frame may come before the view is on
        // screen, and an autofocused field would otherwise never open the keyboard.
        if (!_proxy.IsFirstResponder && _view.Window?.IsKeyWindow == true) _proxy.BecomeFirstResponder();
    }

    /// <summary>
    /// Describes what is being edited to the keyboard: prose for a field, with autocorrection and
    /// suggestions; nothing of the kind for a field that hides what is typed, or for code. A
    /// keyboard already open takes the new description; one that has to become a secure keyboard
    /// is closed and opened again, which is when iOS reads that.
    /// </summary>
    private void Configure()
    {
        var field = _session.Target == TextInputTarget.Text;
        var prose = field && !_session.Obscure;
        if (_proxy.IsFirstResponder && _proxy.SecureTextEntry != _session.Obscure) Resign();
        _proxy.SecureTextEntry = _session.Obscure;
        _proxy.AutocorrectionType = prose ? UITextAutocorrectionType.Default : UITextAutocorrectionType.No;
        _proxy.SpellCheckingType = prose ? UITextSpellCheckingType.Default : UITextSpellCheckingType.No;
        _proxy.SmartQuotesType = prose ? UITextSmartQuotesType.Default : UITextSmartQuotesType.No;
        _proxy.SmartDashesType = prose ? UITextSmartDashesType.Default : UITextSmartDashesType.No;
        _proxy.SmartInsertDeleteType = prose ? UITextSmartInsertDeleteType.Default : UITextSmartInsertDeleteType.No;
        _proxy.AutocapitalizationType = UITextAutocapitalizationType.None;
        // A single line ends on the keyboard's own key, which submits it.
        _proxy.ReturnKeyType = field && _host.TextTarget?.Lines <= 1 ? UIReturnKeyType.Done : UIReturnKeyType.Default;
        Show(field ? _session.Value : TextEditingValue.Collapsed("", 0));
        if (_proxy.IsFirstResponder) _proxy.ReloadInputViews();
    }

    /// <summary>Puts a state into the proxy. Its notifications are then the shell's, not the
    /// keyboard's, and go nowhere.</summary>
    private void Show(TextEditingValue value)
    {
        _applying = true;
        try
        {
            // The host ended the composition (the caret moved, the app rewrote the text): what is
            // marked becomes plain text, which is what the keyboard is then told.
            if (_proxy.MarkedTextRange is not null && !value.HasComposing) _proxy.UnmarkText();
            if (_proxy.Text != value.Text) _proxy.Text = value.Text;
            _proxy.SelectedRange = new NSRange(value.SelectionStart, value.SelectionEnd - value.SelectionStart);
        }
        finally
        {
            _applying = false;
        }
    }

    private void Resign()
    {
        _resigning = true;
        try
        {
            _proxy.ResignFirstResponder();
        }
        finally
        {
            _resigning = false;
        }
    }

    /// <summary>The proxy stands at the caret, so what the system draws for it (a composition's
    /// candidates, dictation) appears at the field.</summary>
    private void Place()
    {
        if (_host.CaretRect() is not { } caret) return;
        var frame = new CGRect(caret.X, caret.Y, MathF.Max(caret.Width, 1), MathF.Max(caret.Height, 1));
        if (_proxy.Frame != frame) _proxy.Frame = frame;
    }

    // ---- The keyboard edits --------------------------------------------------------------------

    /// <summary>UIKit changed the proxy's text or selection.</summary>
    private void Edited()
    {
        if (_applying) return;
        switch (_session.Target)
        {
            case TextInputTarget.Text:
                var selection = _proxy.SelectedRange;
                var (composingStart, composingEnd) = Marked();
                var value = new TextEditingValue(_proxy.Text ?? "", (int)selection.Location,
                    (int)(selection.Location + selection.Length), composingStart, composingEnd);
                if (value != _session.Value) _session.Update(value);
                break;
            case TextInputTarget.Keys:
                Composed();
                break;
        }
    }

    /// <summary>
    /// A composition for a surface that takes keys builds in the proxy, as UIKit composes it, and
    /// reaches the host as its marked text; once committed, what it made is typed and the proxy is
    /// emptied for the next.
    /// </summary>
    private void Composed()
    {
        if (_proxy.MarkedTextRange is { } marked)
        {
            _host.SetMarkedText(_proxy.TextInRange(marked) ?? "");
            return;
        }
        var text = _proxy.Text ?? "";
        if (text.Length > 0) _host.CommitText(text);
        else if (_host.HasMarkedText) _host.SetMarkedText("");
        Show(TextEditingValue.Collapsed("", 0));
    }

    private (int Start, int End) Marked()
    {
        if (_proxy.MarkedTextRange is not { } marked) return (-1, -1);
        var start = _proxy.GetOffsetFromPosition(_proxy.BeginningOfDocument, marked.Start);
        var end = _proxy.GetOffsetFromPosition(_proxy.BeginningOfDocument, marked.End);
        return ((int)start, (int)end);
    }

    /// <summary>
    /// Before UIKit edits the proxy. A field's single line ends on the return key, which submits it
    /// instead of breaking the line. A surface that takes keys is typed into directly, and the
    /// proxy stays empty, unless a composition is building in it.
    /// </summary>
    private bool ShouldChange(UITextView view, NSRange range, string replacement)
    {
        if (_applying) return true;
        switch (_session.Target)
        {
            case TextInputTarget.Text:
                if (replacement != "\n" || _host.TextTarget?.Lines > 1) return true;
                _host.KeyDown("Enter");
                return false;
            case TextInputTarget.Keys:
                if (_proxy.MarkedTextRange is not null) return true;
                if (replacement == "\n") _host.KeyDown("Enter");
                else if (replacement.Length > 0) _host.TextInput(replacement);
                return false;
            default:
                return false;
        }
    }

    private bool Backspace()
    {
        if (_session.Target != TextInputTarget.Keys || _proxy.MarkedTextRange is not null) return false;
        _host.KeyDown("Backspace");
        return true;
    }

    /// <summary>
    /// A key from an attached keyboard. A field keeps the keys UIKit edits it with (arrows,
    /// Backspace, Return), and the host takes Tab, Escape and every ⌘ or ⌃ chord, the app's
    /// shortcuts among them. A surface that takes keys takes every key that types nothing; what
    /// types goes through UIKit, so it can compose. An edit the host makes to a field goes into the
    /// proxy at once, so UIKit's next edit starts from it.
    /// </summary>
    private bool Key(UIKey key)
    {
        if (_session.Target == TextInputTarget.None) return false;
        var modifiers = IosKeys.Modifiers(key.ModifierFlags);
        var chord = (modifiers & (KeyModifiers.Command | KeyModifiers.Control)) != 0;
        var named = IosKeys.NameOf(key);
        var name = chord ? named ?? IosKeys.Character(key)
            : _session.Target == TextInputTarget.Keys || named is "Tab" or "Escape" ? named
            : null;
        if (name is not { Length: > 0 } || !_host.KeyDown(name, modifiers)) return false;
        if (_session.Target == TextInputTarget.Text && _host.EditingValue is { } value) Show(value);
        return true;
    }

    /// <summary>
    /// The keyboard's frame, in the screen's coordinates, taken into the view's: the view need not
    /// fill the screen. A keyboard that stops short of the view's bottom floats (an iPad's undocked
    /// or split keyboard) and takes no bottom from the layout.
    /// </summary>
    private void Covered(CGRect frame)
    {
        var local = _view.ConvertRectFromCoordinateSpace(frame, UIScreen.MainScreen.CoordinateSpace);
        var bounds = _view.Bounds;
        var overlap = local.Bottom >= bounds.Bottom - 1 ? MathF.Max(0, (float)(bounds.Bottom - local.Top)) : 0;
        _host.ViewInsets = new EdgeInsets(0, 0, 0, overlap);
    }

    public void Dispose()
    {
        _moved.Dispose();
        _hidden.Dispose();
        _proxy.RemoveFromSuperview();
        _proxy.Dispose();
    }
}
