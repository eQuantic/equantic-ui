namespace eQuantic.UI.Code;

/// <summary>What asked a signature provider, as LSP's <c>SignatureHelpTriggerKind</c> says it.</summary>
public enum CodeSignatureTrigger
{
    /// <summary>Asked for: ⌘⇧Space (Ctrl+Shift+Space).</summary>
    Invoked,

    /// <summary>A trigger character typed, or a retrigger character while the panel shows.</summary>
    Character,

    /// <summary>The text changed or the caret moved while the panel showed.</summary>
    ContentChange,
}
