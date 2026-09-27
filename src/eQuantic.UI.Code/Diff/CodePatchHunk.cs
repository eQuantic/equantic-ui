namespace eQuantic.UI.Code;

/// <summary>
/// One hunk of a unified diff: <see cref="OriginalCount"/> lines of the original from
/// <see cref="OriginalStart"/> and <see cref="ModifiedCount"/> lines of the modified text from
/// <see cref="ModifiedStart"/>, both counted from 0 as a document counts its lines (the patch's own
/// header counts from 1). A side of no lines starts where its lines would go. <see cref="Section"/> is
/// what follows the header, the function a hunk is in when the tool that wrote it knew, and
/// <see cref="Header"/> the header line itself, as the patch wrote it.
/// </summary>
public sealed record CodePatchHunk(
    int OriginalStart,
    int OriginalCount,
    int ModifiedStart,
    int ModifiedCount,
    string Section,
    IReadOnlyList<CodePatchLine> Lines)
{
    /// <summary>The <c>@@ -l,s +l,s @@ section</c> line, as written: what a view of the patch shows
    /// where the lines before the hunk were left out, as a code review does.</summary>
    public string Header { get; init; } = "";
}
