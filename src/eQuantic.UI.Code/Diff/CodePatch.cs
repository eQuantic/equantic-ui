namespace eQuantic.UI.Code;

/// <summary>
/// Reads a unified diff, the text <c>git diff</c> and <c>diff -u</c> write: its files and, in each,
/// its hunks. What a diff view draws when it has the patch and not the two files, as an IDE's agent
/// has after running <c>git diff</c>.
/// <para>
/// A hunk ends when it has as many lines as its header counted on each side, which is how a line
/// that happens to begin with <c>---</c> or <c>@@</c> stays a line of the hunk. Lines the format
/// adds around the hunks (<c>index</c>, modes, renames, similarity) are read for what they say about
/// the paths and skipped otherwise, and <c>\ No newline at end of file</c> belongs to no side.
/// </para>
/// </summary>
public static class CodePatch
{
    /// <summary>The files of <paramref name="text"/>, in the order the patch gives them.</summary>
    public static IReadOnlyList<CodePatchFile> Parse(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var files = new List<CodePatchFile>();
        string? originalPath = null;
        string? modifiedPath = null;
        var binary = false;
        var hunks = new List<CodePatchHunk>();
        var open = false;

        void Close()
        {
            if (open && (hunks.Count > 0 || binary || originalPath is not null || modifiedPath is not null))
                files.Add(new CodePatchFile(originalPath, modifiedPath, hunks) { Binary = binary });
            originalPath = null;
            modifiedPath = null;
            binary = false;
            hunks = new List<CodePatchHunk>();
            open = false;
        }

        var i = 0;
        while (i < lines.Length)
        {
            var line = lines[i];
            if (line.StartsWith("diff --git ", StringComparison.Ordinal))
            {
                Close();
                open = true;
                // The paths from the header, until the --- and +++ lines say better: a rename or a
                // mode change with no hunk has nothing else to name its file.
                var (a, b) = GitHeaderPaths(line["diff --git ".Length..]);
                originalPath = a;
                modifiedPath = b;
                i++;
                continue;
            }
            if (line.StartsWith("--- ", StringComparison.Ordinal) && i + 1 < lines.Length
                && lines[i + 1].StartsWith("+++ ", StringComparison.Ordinal))
            {
                // A plain diff -u has no "diff --git" line: its --- starts the file.
                if (!open || hunks.Count > 0) Close();
                open = true;
                originalPath = PathOf(line[4..]);
                modifiedPath = PathOf(lines[i + 1][4..]);
                i += 2;
                continue;
            }
            if (line.StartsWith("rename from ", StringComparison.Ordinal)) originalPath = line["rename from ".Length..];
            else if (line.StartsWith("rename to ", StringComparison.Ordinal)) modifiedPath = line["rename to ".Length..];
            else if (line.StartsWith("Binary files ", StringComparison.Ordinal)) binary = true;
            else if (line.StartsWith("@@ ", StringComparison.Ordinal) && Header(line) is { } header)
            {
                open = true;
                i = ReadHunk(lines, i + 1, header, line, hunks);
                continue;
            }
            i++;
        }
        Close();
        return files;
    }

    /// <summary>The numbers of a hunk's header, <c>@@ -l,s +l,s @@ section</c>, a count left out
    /// being 1. Null when the line is not one.</summary>
    private static (int OriginalLine, int OriginalCount, int ModifiedLine, int ModifiedCount, string Section)? Header(string line)
    {
        var close = line.IndexOf(" @@", 3, StringComparison.Ordinal);
        if (close < 0) return null;
        var parts = line[3..close].Split(' ');
        if (parts.Length != 2 || !parts[0].StartsWith('-') || !parts[1].StartsWith('+')) return null;
        if (Range(parts[0][1..]) is not { } original || Range(parts[1][1..]) is not { } modified) return null;
        var section = close + 3 < line.Length ? line[(close + 3)..].TrimStart() : "";
        return (original.Line, original.Count, modified.Line, modified.Count, section);
    }

    private static (int Line, int Count)? Range(string text)
    {
        var comma = text.IndexOf(',');
        var lineText = comma < 0 ? text : text[..comma];
        var countText = comma < 0 ? "1" : text[(comma + 1)..];
        if (!int.TryParse(lineText, out var line) || !int.TryParse(countText, out var count)) return null;
        return (line, count);
    }

    /// <summary>Reads a hunk's lines from <paramref name="at"/> until each side has the lines its
    /// header counted, and answers where the next line of the patch is.</summary>
    private static int ReadHunk(string[] lines, int at,
        (int OriginalLine, int OriginalCount, int ModifiedLine, int ModifiedCount, string Section) header,
        string headerLine, List<CodePatchHunk> hunks)
    {
        var body = new List<CodePatchLine>();
        var original = 0;
        var modified = 0;
        var i = at;
        while (i < lines.Length && (original < header.OriginalCount || modified < header.ModifiedCount))
        {
            var line = lines[i];
            if (line.StartsWith('\\')) { i++; continue; }   // "\ No newline at end of file"
            // Some tools write an empty context line as nothing at all, not as a lone space.
            var mark = line.Length > 0 ? line[0] : ' ';
            var content = line.Length > 0 ? line[1..] : "";
            if (mark == '-') { body.Add(new CodePatchLine(CodePatchLineKind.Removed, content)); original++; }
            else if (mark == '+') { body.Add(new CodePatchLine(CodePatchLineKind.Added, content)); modified++; }
            else if (mark == ' ') { body.Add(new CodePatchLine(CodePatchLineKind.Context, content)); original++; modified++; }
            else break;
            i++;
        }
        while (i < lines.Length && lines[i].StartsWith('\\')) i++;
        // The header counts from 1, and a side of no lines names the line it comes AFTER.
        hunks.Add(new CodePatchHunk(
            header.OriginalCount == 0 ? header.OriginalLine : header.OriginalLine - 1, header.OriginalCount,
            header.ModifiedCount == 0 ? header.ModifiedLine : header.ModifiedLine - 1, header.ModifiedCount,
            header.Section, body) { Header = headerLine });
        return i;
    }

    /// <summary>A path from a --- or +++ line: null for <c>/dev/null</c>, git's <c>a/</c> and
    /// <c>b/</c> taken off, and a timestamp after a tab (diff -u writes one) dropped.</summary>
    private static string? PathOf(string text)
    {
        var tab = text.IndexOf('\t');
        var path = tab < 0 ? text : text[..tab];
        if (path == "/dev/null") return null;
        if (path.StartsWith("a/", StringComparison.Ordinal) || path.StartsWith("b/", StringComparison.Ordinal))
            return path[2..];
        return path;
    }

    /// <summary>The two paths of a <c>diff --git a/x b/y</c> line. A path with a space in it is
    /// ambiguous there, so this splits at the last " b/", and the --- and +++ lines, when the file
    /// has hunks, say it better anyway.</summary>
    private static (string? Original, string? Modified) GitHeaderPaths(string text)
    {
        var split = text.LastIndexOf(" b/", StringComparison.Ordinal);
        if (split < 0) return (null, null);
        return (PathOf(text[..split]), PathOf(text[(split + 1)..]));
    }
}
