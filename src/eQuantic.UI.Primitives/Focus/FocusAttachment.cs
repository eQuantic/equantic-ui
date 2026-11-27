namespace eQuantic.UI.Primitives;

/// <summary>
/// One <see cref="FocusNode"/> a stop stands under, as the latest build wrote it (#1897): Flutter's
/// <c>FocusAttachment</c>, which a <c>Focus</c> widget's state holds to hang its node in the tree, and which here a
/// realizer hands the <see cref="FocusManager"/> for each <c>Focus</c> and <c>FocusScope</c> node above a stop. The
/// attachment is made at the POSITION, the element on Photon and the DOM element on the web (Flutter's second
/// tree), never by the node the app holds, which gains no parent.
/// </summary>
/// <param name="Node">The node the <c>Focus</c> or <c>FocusScope</c> holds: the app's, or one the realizer kept at
/// the position for a <c>Focus</c> given none, as Flutter's <c>Focus</c> makes one in its state.</param>
/// <param name="OnFocusChange">The <c>onFocusChange</c> the latest build wrote, called with <see cref="FocusNode.HasFocus"/>
/// when it changes; null for none.</param>
/// <param name="Autofocus">Whether the latest build asked for the focus as the position first appeared
/// (Flutter's <c>autofocus</c>), asked once per position.</param>
public readonly record struct FocusAttachment(FocusNode Node, Action<bool>? OnFocusChange, bool Autofocus);
