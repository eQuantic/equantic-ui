namespace eQuantic.UI.Code;

/// <summary>
/// One file of a patch: its path on each side, null where the file did not exist there (a file
/// added or deleted), and its hunks. A binary file has no hunks, and says so.
/// </summary>
public sealed record CodePatchFile(string? OriginalPath, string? ModifiedPath, IReadOnlyList<CodePatchHunk> Hunks)
{
    /// <summary>Whether the patch only said that the file's bytes differ.</summary>
    public bool Binary { get; init; }
}
