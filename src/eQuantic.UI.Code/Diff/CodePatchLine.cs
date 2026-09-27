namespace eQuantic.UI.Code;

/// <summary>One line of a hunk: what it is, and its text without the mark that said so.</summary>
public readonly record struct CodePatchLine(CodePatchLineKind Kind, string Text);
