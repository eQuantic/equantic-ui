namespace eQuantic.UI.Code;

/// <summary>What hovering over a range explains.</summary>
public sealed record CodeHover(CodeRange Range, string Text)
{
    /// <summary>A code-formatted first line: a signature, a resolved type.</summary>
    public string? Signature { get; init; }
}
