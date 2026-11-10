using System.Text;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Testing;

/// <summary>
/// What a screen reader announces for one stop of a frame, in the vocabulary's words: a
/// <see cref="SemanticRole"/>, never an ARIA string, so a test reads the same whatever the web
/// writes for it. The fields are the ones a <see cref="SemanticNode"/> announces, without the
/// position's id and box, which a frame read from markup does not have.
/// </summary>
public sealed record Announcement
{
    /// <summary>What kind of thing it is, which decides how it is announced.</summary>
    public required SemanticRole Role { get; init; }

    /// <summary>Its accessible name, whitespace collapsed; empty when it has none.</summary>
    public required string Name { get; init; }

    /// <summary>Its current value, for the roles that carry one (a field's text, a slider's or a
    /// progress's position, a combo box's choice); null when it has none, which for a progress is
    /// what an indeterminate one announces.</summary>
    public string? Value { get; init; }

    /// <summary>Whether it refuses interaction.</summary>
    public bool Disabled { get; init; }

    /// <summary>A check's answer, for a checkbox, a switch or a radio; null where nothing is asked.</summary>
    public SemanticCheck? Checked { get; init; }

    /// <summary>Whether this one of a set is picked (a tab, an option); null where nothing is picked.</summary>
    public bool? Selected { get; init; }

    /// <summary>Whether a disclosure is open; null where nothing opens.</summary>
    public bool? Expanded { get; init; }

    /// <summary>Whether this is the destination the user is on.</summary>
    public bool Current { get; init; }

    /// <summary>The level in the outline, 1 to 6, for a heading; 0 for anything else.</summary>
    public int HeadingLevel { get; init; }

    /// <summary>The announcement as a failure message names it: <c>Button "Save" (disabled)</c>.</summary>
    public override string ToString()
    {
        var text = new StringBuilder($"{Role} \"{Name}\"");
        var states = new List<string>();
        if (HeadingLevel > 0) states.Add($"level {HeadingLevel}");
        if (Value is not null) states.Add($"value \"{Value}\"");
        if (Disabled) states.Add("disabled");
        if (Checked is { } check) states.Add($"checked {check}");
        if (Selected is { } selected) states.Add(selected ? "selected" : "not selected");
        if (Expanded is { } expanded) states.Add(expanded ? "expanded" : "collapsed");
        if (Current) states.Add("current");
        if (states.Count > 0) text.Append(" (").Append(string.Join(", ", states)).Append(')');
        return text.ToString();
    }
}
