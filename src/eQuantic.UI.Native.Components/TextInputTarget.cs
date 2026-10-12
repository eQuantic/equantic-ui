namespace eQuantic.UI.Native.Components;

/// <summary>What a phone's keyboard is editing, as a <see cref="TextInputSession"/> reads it.</summary>
public enum TextInputTarget
{
    /// <summary>Nothing in the frame holds the caret: the keyboard is down.</summary>
    None,

    /// <summary>A field: the keyboard edits its text as a document, through
    /// <see cref="PhotonHost.UpdateEditingValue"/>, with suggestions, autocorrection and composition.</summary>
    Text,

    /// <summary>A code surface or a sheet: it keeps its text to itself, so the keyboard types keys and
    /// committed text into it, through <see cref="PhotonHost.KeyDown"/> and
    /// <see cref="PhotonHost.TextInput"/>, and suggests nothing.</summary>
    Keys,
}
