namespace eQuantic.UI.Primitives;

/// <summary>
/// Single-line text ENTRY (the B9/B10 primitive): value + placeholder + change/submit/focus
/// callbacks. The web realizer lowers it to a real chrome-less <c>&lt;input&gt;</c> (the browser
/// owns caret/selection/IME); the CONTAINER chrome (border, states, label) belongs to the
/// composing component. Native renders the layout-correct one-line frame through the W4 text
/// placeholder pattern — caret/selection/IME land at M4, and the spec fixes the visual contract
/// NOW so forms don't re-layout later (spec B9).
/// </summary>
public sealed class TextEntry : VisualNode
{
    public override string NodeKind => "textEntry";

    public TextEntry(string value, Action<string>? onChanged = null)
    {
        Value = value;
        OnChanged = onChanged;
    }

    public string Value { get; init; }
    public Action<string>? OnChanged { get; init; }

    /// <summary>Shown in TextMuted while <see cref="Value"/> is empty — never a label substitute.</summary>
    public string? Placeholder { get; init; }

    /// <summary>The accessible NAME assistive tech announces (aria-label on web, the AX label on
    /// native). A placeholder disappears the moment the field holds text — it is a hint, not a
    /// name — so a field without a visible label states one here.</summary>
    public string? Label { get; init; }

    /// <summary>The accessible DESCRIPTION — the caption text (helper or error) the composing
    /// component shows beside the field. The web realizer keeps a visually hidden twin of it
    /// INSIDE the entry (the association target, and a polite live region so swaps announce),
    /// because the visible caption is a sibling node the entry cannot reference. Native fence:
    /// joins when the semantics tree carries descriptions.</summary>
    public string? Description { get; init; }

    /// <summary>The value currently FAILS validation — surfaced as a STATE (web
    /// <c>aria-invalid</c>), never worded into the name or description. Native fence: the
    /// semantics tree has no invalid bit yet.</summary>
    public bool Invalid { get; init; }

    /// <summary>Keyboard submit (Enter / the keyboard's return action).</summary>
    public Action? OnSubmit { get; init; }

    /// <summary>Focus transitions — the composing component's state hook (focused border etc.).</summary>
    public Action<bool>? OnFocusChanged { get; init; }

    public bool Disabled { get; init; }

    /// <summary>Password entry: glyphs render obscured (web <c>type=password</c>).</summary>
    public bool Obscure { get; init; }

    /// <summary>Type role of the entry text (spec: TextInput rides BodyL, SearchField BodyM).</summary>
    public TypeRole Role { get; init; } = TypeRole.BodyL;

    /// <summary>
    /// Takes focus when it MOUNTS (the command-palette contract: the field is ready to type into
    /// the instant the dialog appears). Web sets the attribute AND focuses on mount — the attribute
    /// alone only fires on the initial document parse, never on a client-rendered dialog. Use once
    /// per surface; native fence: the host focus system.
    /// </summary>
    public bool Autofocus { get; init; }

    /// <summary>
    /// How many lines of text the field holds. <c>1</c> (the default) is the single-line entry;
    /// anything greater makes it a MULTI-LINE field of exactly that many lines tall — the message
    /// box of a contact form, a note, a description. The count is the field's HEIGHT, not a limit:
    /// content beyond it scrolls (web <c>textarea rows</c>). Native measures the same line count,
    /// so a form's geometry matches before the caret/IME stack lands.
    /// </summary>
    public int Lines { get; init; } = 1;

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
