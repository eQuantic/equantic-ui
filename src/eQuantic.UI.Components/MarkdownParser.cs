namespace eQuantic.UI.Components;

/// <summary>A matched inline link: label, destination, and where scanning resumes.</summary>
internal sealed class MarkdownLinkMatch
{
    public string Label { get; set; } = "";
    public string Href { get; set; } = "";
    public int End { get; set; }
}

/// <summary>A matched list bullet: what the gutter shows, and the content after it.</summary>
internal sealed class MarkdownBulletMatch
{
    public string Marker { get; set; } = "•";
    public string Content { get; set; } = "";
}

/// <summary>
/// The markdown parser behind <see cref="Markdown"/>, promoted from the documentation site that
/// proved it in production. Two deliberate shapes govern it:
/// <para>
/// The INLINE half is a scanner rather than a regex sweep — one left-to-right walk where a code
/// span claims its characters first, so prose ABOUT markdown (<c>`**`</c> inside backticks, an
/// asterisk inside a code span) stays literal. The BLOCK half is line-oriented and single-pass —
/// a fence claims its lines before anything else looks at them, which is what keeps a <c>#</c> or
/// a <c>|</c> inside a code sample from becoming a heading or a table.
/// </para>
/// <para>
/// It runs on EVERY side of the framework: the same C# parses during SSR and on Photon, and its
/// transpiled twin parses in the browser — one implementation is what makes the server's render
/// and the client's re-render agree by construction. That is also why it is written plainly
/// (index walks, string slices, no regex): the whole method body must cross the compiler.
/// </para>
/// v1 fences, deliberate: emphasis is asterisk-only (<c>_underscores_</c> stay literal — prose
/// about code is full of identifiers_with_underscores); an image renders as its alt text linking
/// to the source (an <see cref="Primitives.Image"/> is an explicitly sized slot by spec, and
/// markdown carries no dimensions); raw HTML never passes through (an injection surface on web,
/// meaningless on Photon); fenced code only, no four-space indent blocks.
/// </summary>
public static class MarkdownParser
{
    // ---- Blocks ------------------------------------------------------------------------------

    /// <summary>Parses a whole document into its blocks.</summary>
    public static List<MarkdownBlock> Parse(string source)
    {
        var blocks = new List<MarkdownBlock>();
        if (string.IsNullOrEmpty(source)) return blocks;

        var lines = StripComments(source.Replace("\r\n", "\n").Split('\n'));
        var paragraph = "";
        // Two sections can honestly carry the same name. Left alone they mint the same anchor,
        // and a table of contents then scrolls to the other one's section.
        var usedIds = new HashSet<string>();

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            // A fence owns everything up to its close, markers and all.
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                paragraph = FlushParagraph(paragraph, blocks);
                var lang = trimmed[3..].Trim();
                var body = new List<string>();
                i++;
                while (i < lines.Count && !lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    body.Add(lines[i]);
                    i++;
                }
                blocks.Add(new MarkdownBlock
                {
                    Kind = MarkdownBlockKind.Code,
                    Lang = lang.Length == 0 ? "text" : lang,
                    Raw = string.Join("\n", body),
                });
                continue;
            }

            if (trimmed.Length == 0)
            {
                paragraph = FlushParagraph(paragraph, blocks);
                continue;
            }

            if (trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                var level = 0;
                while (level < trimmed.Length && trimmed[level] == '#') level++;
                // The space is CommonMark's rule, and it is what keeps a #hashtag in prose from
                // becoming a heading.
                if (level <= 6 && level < trimmed.Length && trimmed[level] == ' ')
                {
                    paragraph = FlushParagraph(paragraph, blocks);
                    var text = trimmed[level..].Trim();
                    var id = Slug(text);
                    var unique = id;
                    var n = 1;
                    while (usedIds.Contains(unique))
                    {
                        n++;
                        unique = id + "-" + n;
                    }
                    usedIds.Add(unique);
                    blocks.Add(new MarkdownBlock
                    {
                        Kind = MarkdownBlockKind.Heading,
                        Level = level > 4 ? 4 : level,
                        Text = Strip(text),
                        Runs = Inline(text),
                        Id = unique,
                    });
                    continue;
                }
            }

            if (trimmed == "---" || trimmed == "***" || trimmed == "___")
            {
                paragraph = FlushParagraph(paragraph, blocks);
                blocks.Add(new MarkdownBlock { Kind = MarkdownBlockKind.Rule });
                continue;
            }

            // A table is its header, its alignment row, and every row after — taken together,
            // because a lone `|` line is prose about a pipe far more often than it is a table.
            if (trimmed.StartsWith("|", StringComparison.Ordinal) && i + 1 < lines.Count
                && IsAlignmentRow(lines[i + 1]))
            {
                paragraph = FlushParagraph(paragraph, blocks);
                var table = new MarkdownBlock { Kind = MarkdownBlockKind.Table };
                foreach (var cell in SplitRow(trimmed))
                    table.Head.Add(new MarkdownCell { Runs = Inline(cell) });
                i += 2;
                while (i < lines.Count && lines[i].Trim().StartsWith("|", StringComparison.Ordinal))
                {
                    var row = new MarkdownRow();
                    foreach (var cell in SplitRow(lines[i].Trim()))
                        row.Cells.Add(new MarkdownCell { Runs = Inline(cell) });
                    table.Rows.Add(row);
                    i++;
                }
                i--;
                blocks.Add(table);
                continue;
            }

            if (trimmed.StartsWith(">", StringComparison.Ordinal))
            {
                paragraph = FlushParagraph(paragraph, blocks);
                var quoted = "";
                while (i < lines.Count && lines[i].TrimStart().StartsWith(">", StringComparison.Ordinal))
                {
                    var q = lines[i].TrimStart();
                    q = q[1..].Trim();
                    quoted = quoted.Length == 0 ? q : quoted + " " + q;
                    i++;
                }
                i--;
                blocks.Add(new MarkdownBlock { Kind = MarkdownBlockKind.Quote, Runs = Inline(quoted.Trim()) });
                continue;
            }

            var bullet = BulletOf(line);
            if (bullet != null)
            {
                paragraph = FlushParagraph(paragraph, blocks);
                var list = new MarkdownBlock { Kind = MarkdownBlockKind.List };
                while (i < lines.Count)
                {
                    var mark = BulletOf(lines[i]);
                    if (mark == null)
                    {
                        // A wrapped continuation line belongs to the item above it.
                        if (list.Items.Count > 0 && lines[i].StartsWith("  ", StringComparison.Ordinal)
                            && lines[i].Trim().Length > 0
                            && !lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal))
                        {
                            var last = list.Items[^1];
                            foreach (var run in Inline(" " + lines[i].Trim())) last.Runs.Add(run);
                            i++;
                            continue;
                        }
                        break;
                    }
                    var indent = lines[i].Length - lines[i].TrimStart().Length;
                    list.Items.Add(new MarkdownListItem
                    {
                        Runs = Inline(mark.Content),
                        Depth = indent >= 2 ? 1 : 0,
                        Marker = mark.Marker,
                    });
                    i++;
                }
                i--;
                blocks.Add(list);
                continue;
            }

            paragraph = paragraph.Length == 0 ? trimmed : paragraph + " " + trimmed;
        }

        FlushParagraph(paragraph, blocks);
        return blocks;
    }

    /// <summary>The heading's anchor: lowercase, letters and digits kept, everything else one dash.</summary>
    public static string Slug(string text)
    {
        var plain = Strip(text).ToLowerInvariant();
        var slug = "";
        for (var i = 0; i < plain.Length; i++)
        {
            var ch = plain[i];
            if (char.IsLetter(ch) || char.IsDigit(ch)) slug += ch;
            else if (slug.Length > 0 && slug[^1] != '-') slug += "-";
        }
        while (slug.Length > 0 && slug[^1] == '-') slug = slug[..(slug.Length - 1)];
        return slug;
    }

    private static string FlushParagraph(string paragraph, List<MarkdownBlock> blocks)
    {
        var joined = paragraph.Trim();
        if (joined.Length == 0) return "";
        blocks.Add(new MarkdownBlock { Kind = MarkdownBlockKind.Paragraph, Runs = Inline(joined) });
        return "";
    }

    /// <summary>
    /// HTML comments, gone before the parser sees a line — except inside a fence, where they are
    /// code. A reader has no use for them; left alone one renders as prose, mid-sentence. A
    /// comment may span lines, and half a tag either side of one must not leak.
    /// </summary>
    private static List<string> StripComments(string[] lines)
    {
        var clean = new List<string>();
        var fenced = false;
        var open = false;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                fenced = !fenced;
                clean.Add(line);
                continue;
            }
            if (fenced)
            {
                clean.Add(line);
                continue;
            }

            var text = line;
            if (open)
            {
                var close = text.IndexOf("-->", StringComparison.Ordinal);
                if (close < 0)
                {
                    clean.Add("");
                    continue;
                }
                text = text[(close + 3)..];
                open = false;
            }
            while (true)
            {
                var start = text.IndexOf("<!--", StringComparison.Ordinal);
                if (start < 0) break;
                var end = text.IndexOf("-->", start, StringComparison.Ordinal);
                if (end < 0)
                {
                    text = text[..start];
                    open = true;
                    break;
                }
                text = text[..start] + text[(end + 3)..];
            }
            clean.Add(text.TrimEnd());
        }
        return clean;
    }

    private static bool IsAlignmentRow(string line)
    {
        var t = line.Trim();
        if (!t.StartsWith("|", StringComparison.Ordinal)) return false;
        var hasDash = false;
        for (var i = 0; i < t.Length; i++)
        {
            var ch = t[i];
            if (ch == '-') hasDash = true;
            else if (ch != '|' && ch != ':' && ch != ' ') return false;
        }
        return hasDash;
    }

    private static List<string> SplitRow(string line)
    {
        var cells = new List<string>();
        var t = line.Trim();
        if (t.Length > 0 && t[0] == '|') t = t[1..];
        if (t.Length > 0 && t[^1] == '|') t = t[..(t.Length - 1)];

        var cell = "";
        var inCode = false;
        for (var i = 0; i < t.Length; i++)
        {
            var ch = t[i];
            if (ch == '`') inCode = !inCode;
            // A pipe inside a code span is content — API tables document `A | B` unions.
            if (ch == '|' && !inCode)
            {
                cells.Add(cell.Trim());
                cell = "";
                continue;
            }
            cell += ch;
        }
        cells.Add(cell.Trim());
        return cells;
    }

    private static MarkdownBulletMatch? BulletOf(string line)
    {
        var t = line.TrimStart();
        if (t.StartsWith("- ", StringComparison.Ordinal) || t.StartsWith("* ", StringComparison.Ordinal))
            return new MarkdownBulletMatch { Marker = "•", Content = t[2..].Trim() };
        var digits = 0;
        while (digits < t.Length && char.IsDigit(t[digits])) digits++;
        if (digits > 0 && digits + 1 < t.Length && t[digits] == '.' && t[digits + 1] == ' ')
            return new MarkdownBulletMatch { Marker = t[..digits] + ".", Content = t[(digits + 2)..].Trim() };
        return null;
    }

    // ---- Inlines -----------------------------------------------------------------------------

    /// <summary>
    /// The inline half: <c>**bold**</c>, <c>*italic*</c>, <c>`code`</c>, <c>[text](href)</c>,
    /// <c>![alt](src)</c>. One left-to-right walk, code spans claimed first — a <c>**</c> inside
    /// backticks stays two asterisks.
    /// </summary>
    public static List<MarkdownRun> Inline(string text)
    {
        var runs = new List<MarkdownRun>();
        if (string.IsNullOrEmpty(text)) return runs;

        var buffer = "";
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];

            if (c == '`')
            {
                var end = text.IndexOf('`', i + 1);
                if (end > i)
                {
                    buffer = FlushText(runs, buffer);
                    runs.Add(new MarkdownRun { Text = text[(i + 1)..end], Code = true });
                    i = end + 1;
                    continue;
                }
            }
            else if (c == '!' && i + 1 < text.Length && text[i + 1] == '[')
            {
                // v1: the image's alt text, linking to the source. An Image node is an explicitly
                // sized slot by spec, and markdown carries no dimensions to size one with.
                var image = MatchLink(text, i + 1);
                if (image != null)
                {
                    buffer = FlushText(runs, buffer);
                    AddLinkRuns(runs, image.Label.Length == 0 ? image.Href : image.Label, image.Href);
                    i = image.End;
                    continue;
                }
            }
            else if (c == '[')
            {
                var link = MatchLink(text, i);
                if (link != null)
                {
                    buffer = FlushText(runs, buffer);
                    AddLinkRuns(runs, link.Label, link.Href);
                    i = link.End;
                    continue;
                }
            }
            else if (c == '*' && i + 1 < text.Length && text[i + 1] == '*')
            {
                var end = text.IndexOf("**", i + 2, StringComparison.Ordinal);
                if (end > i)
                {
                    buffer = FlushText(runs, buffer);
                    foreach (var run in Inline(text[(i + 2)..end]))
                    {
                        run.Bold = true;
                        runs.Add(run);
                    }
                    i = end + 2;
                    continue;
                }
            }
            else if (c == '*')
            {
                // Scan for the close, stepping OVER `**` pairs: `*Since **0.2.0***` is italic
                // wrapping bold, and taking the first `*` as the close leaves `**` on the page.
                var end = -1;
                for (var scan = i + 1; scan < text.Length; scan++)
                {
                    if (text[scan] != '*') continue;
                    if (scan + 1 < text.Length && text[scan + 1] == '*')
                    {
                        scan++;
                        continue;
                    }
                    end = scan;
                    break;
                }
                // Not italic if it closes immediately (`* *`) or never — those are literals.
                if (end > i + 1)
                {
                    buffer = FlushText(runs, buffer);
                    foreach (var run in Inline(text[(i + 1)..end]))
                    {
                        run.Italic = true;
                        runs.Add(run);
                    }
                    i = end + 1;
                    continue;
                }
            }

            buffer += c;
            i++;
        }

        FlushText(runs, buffer);
        return runs;
    }

    /// <summary>The plain reading of an inline string — what a search index or a lead line shows.</summary>
    public static string Strip(string text)
    {
        var flat = "";
        foreach (var run in Inline(text)) flat += run.Text;
        return flat;
    }

    private static string FlushText(List<MarkdownRun> runs, string buffer)
    {
        if (buffer.Length > 0) runs.Add(new MarkdownRun { Text = buffer });
        return "";
    }

    private static MarkdownLinkMatch? MatchLink(string text, int open)
    {
        var close = text.IndexOf(']', open + 1);
        if (close <= open || close + 1 >= text.Length || text[close + 1] != '(') return null;
        var hrefEnd = text.IndexOf(')', close + 2);
        if (hrefEnd <= close) return null;
        return new MarkdownLinkMatch
        {
            Label = text[(open + 1)..close],
            Href = text[(close + 2)..hrefEnd],
            End = hrefEnd + 1,
        };
    }

    /// <summary>A label carries its own emphasis; every run of it keeps whichever it had.</summary>
    private static void AddLinkRuns(List<MarkdownRun> runs, string label, string href)
    {
        var inner = Inline(label);
        if (inner.Count == 0)
        {
            runs.Add(new MarkdownRun { Text = label, Href = href });
            return;
        }
        foreach (var run in inner)
        {
            run.Href = href;
            runs.Add(run);
        }
    }
}
