using Foundation;
using UIKit;

namespace eQuantic.UI.Native.Shell.iOS;

/// <summary>
/// The text view the keyboard types into. Photon draws every field itself, so nothing on screen is
/// a UIKit text view, and iOS opens its keyboard only for one. This one is invisible, takes no
/// touches and sits at the caret, and UIKit runs the whole text input protocol against it
/// (autocorrection, suggestions, dictation, a composition) while <see cref="PhotonKeyboard"/>
/// keeps it and the host's field in step.
/// </summary>
internal sealed class PhotonTextProxy : UITextView
{
    private readonly HashSet<UIPress> _taken = [];

    internal PhotonTextProxy()
    {
        // Alpha zero keeps it out of hit testing and draws nothing of its own (no caret, no
        // selection, no spelling underline), and a view at alpha zero still takes the keyboard.
        Alpha = 0;
        BackgroundColor = UIColor.Clear;
        TextColor = UIColor.Clear;
        TintColor = UIColor.Clear;
        ScrollEnabled = false;
        // One line, clipped, at the caret: where the system asks it for the caret's place (the
        // candidates of a composition, dictation), the answer is the field's caret.
        TextContainerInset = UIEdgeInsets.Zero;
        TextContainer.LineFragmentPadding = 0;
        TextContainer.MaximumNumberOfLines = 1;
        TextContainer.LineBreakMode = UILineBreakMode.Clip;
        // The field it stands for is in the semantics tree already; this is not a second one.
        IsAccessibilityElement = false;
        AccessibilityElementsHidden = true;
        // iOS 17 writes the word it predicts INTO the text view, greyed: text the field never shows
        // and the app's value never holds.
        if (OperatingSystem.IsIOSVersionAtLeast(17)) InlinePredictionType = UITextInlinePredictionType.No;
    }

    /// <summary>Backspace for a surface that takes keys. The proxy is empty there, so UIKit's own
    /// would delete nothing; true when it was taken.</summary>
    internal Func<bool>? BackspaceTaken { get; set; }

    /// <summary>A key from a keyboard attached to the device, offered to the host before UIKit;
    /// true when the host took it.</summary>
    internal Func<UIKey, bool>? KeyTaken { get; set; }

    public override void DeleteBackward()
    {
        if (BackspaceTaken?.Invoke() == true) return;
        base.DeleteBackward();
    }

    /// <summary>A press the host took is not UIKit's, and neither is its end: UIKit is handed only
    /// the presses it began.</summary>
    public override void PressesBegan(NSSet<UIPress> presses, UIPressesEvent evt)
    {
        var rest = new List<UIPress>();
        foreach (var press in presses)
        {
            if (press.Key is { } key && KeyTaken?.Invoke(key) == true) _taken.Add(press);
            else rest.Add(press);
        }
        if (rest.Count > 0) base.PressesBegan(new NSSet<UIPress>(rest.ToArray()), evt);
    }

    public override void PressesEnded(NSSet<UIPress> presses, UIPressesEvent evt) =>
        Forward(presses, evt, base.PressesEnded);

    public override void PressesCancelled(NSSet<UIPress> presses, UIPressesEvent evt) =>
        Forward(presses, evt, base.PressesCancelled);

    private void Forward(NSSet<UIPress> presses, UIPressesEvent evt, Action<NSSet<UIPress>, UIPressesEvent> next)
    {
        var rest = new List<UIPress>();
        foreach (var press in presses)
            if (!_taken.Remove(press)) rest.Add(press);
        if (rest.Count > 0) next(new NSSet<UIPress>(rest.ToArray()), evt);
    }
}
