namespace eQuantic.UI.Native.Components;

/// <summary>What a phone shell owes its keyboard after a frame (<see cref="TextInputSession.Poll"/>).</summary>
public enum TextInputChange
{
    /// <summary>Nothing: the keyboard already has what the host has.</summary>
    None,

    /// <summary>Something took the caret: the keyboard opens, on <see cref="TextInputSession.Value"/>.</summary>
    Began,

    /// <summary>The caret moved to another field or surface, or the field changed what the keyboard
    /// must be (it hides what is typed now): the keyboard starts over on the new target.</summary>
    Retargeted,

    /// <summary>Nothing holds the caret any more: the keyboard closes.</summary>
    Ended,

    /// <summary>The field's value, selection or composition changed by something other than the
    /// keyboard (the app rewrote the value, a press moved the caret, a key edited it): the keyboard
    /// is told <see cref="TextInputSession.Value"/>, or its next edit undoes the change.</summary>
    ValueChanged,
}
