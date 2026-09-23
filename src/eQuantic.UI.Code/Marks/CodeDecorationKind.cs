namespace eQuantic.UI.Code;

/// <summary>What a decoration DOES to the range it covers.</summary>
public enum CodeDecorationKind : byte
{
    /// <summary>A background wash — a search match, a highlighted symbol.</summary>
    Highlight = 0,
    /// <summary>A wavy underline — a diagnostic, in the diagnostic's colour.</summary>
    Squiggle = 1,
    /// <summary>A box around the range — a matching bracket.</summary>
    Outline = 2,
    /// <summary>A strike-through — deleted in a diff, or unreachable code.</summary>
    Strike = 3,
}
