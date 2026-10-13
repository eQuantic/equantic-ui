using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The ledger of a tree: the chronology in <c>docs/LEDGER.md</c>, the events waiting under
/// <c>docs/ledger/</c>, and the record of the chronology as the last fold left it (#892).
/// <para>
/// Every pull request used to write its entry at the chronology's end, so any two open ones
/// conflicted there, and a merge GitHub performs reads no attribute that could tell it the file only
/// grows. An event is a file of its own now, which nobody else writes, and a release folds the files
/// into the chronology: the fold is the chronology's one writer.
/// </para>
/// <para>
/// The fold lives here, in the suite, so that it reads an entry through the reader the guard uses
/// (<see cref="LedgerEntry"/>) and its own cases run on every host. <c>scripts/fold-ledger.sh</c>
/// calls it.
/// </para>
/// </summary>
internal sealed class Ledger(string root)
{
    public const string Document = "docs/LEDGER.md";
    public const string Folder = "docs/ledger";
    public const string Record = "docs/ledger/folded.json";

    /// <summary>The folder's own page, which says what a file beside it is. It is no event.</summary>
    public const string Guide = "README.md";

    private const string Heading = "## Chronology";

    /// <summary>What the record says of itself, for whoever opens it.</summary>
    private static readonly string[] Comment =
    [
        "The chronology of docs/LEDGER.md as the last fold left it (#892): how many entries it holds, and",
        "the SHA-256 of its lines. LedgerTests fails when the chronology differs.",
        "",
        "An event is a file of its own in this folder, never a line written into the chronology, so that",
        "two pull requests never write at the same point. README.md beside this file says how.",
        "",
        "A release folds the files and writes this record: ./scripts/fold-ledger.sh. A deliberate change",
        "to the chronology itself, a citation that moved with its document, is recorded with",
        "EQ_UPDATE_LEDGER=1 on LedgerTests, which folds nothing.",
    ];

    private string DocumentPath => Path.Combine(root, "docs", "LEDGER.md");
    private string FolderPath => Path.Combine(root, "docs", "ledger");
    private string RecordPath => Path.Combine(FolderPath, "folded.json");

    /// <summary>The events waiting for a fold, in the order a fold writes them: by the day each
    /// states, then by name. Entries of one day have no order worth keeping, and a name gives the
    /// same one on every clone.</summary>
    public IReadOnlyList<LedgerEntry> Waiting() =>
        Directory.Exists(FolderPath)
            ? Directory.GetFiles(FolderPath, "*.md")
                .Where(path => Path.GetFileName(path) != Guide)
                .Select(LedgerEntry.Read)
                .OrderBy(entry => entry.Day)
                .ThenBy(entry => entry.Name, StringComparer.Ordinal)
                .ToList()
            : [];

    /// <summary>The chronology as it is: how many entries it holds, and the digest of its lines.</summary>
    public (int Entries, string Sha256) Chronology()
    {
        var (lines, from, to) = Read();
        var chronology = lines[from..to];
        return (
            chronology.Count(line => line.StartsWith("- **", StringComparison.Ordinal)),
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", chronology)))).ToLowerInvariant());
    }

    /// <summary>The chronology as the last fold left it, or nothing when no fold recorded one.</summary>
    public (int Entries, string Sha256)? Recorded()
    {
        if (!File.Exists(RecordPath)) return null;
        var record = JsonNode.Parse(File.ReadAllText(RecordPath))!;
        return (record["entries"]!.GetValue<int>(), record["sha256"]!.GetValue<string>());
    }

    /// <summary>Records the chronology as it is, and folds nothing.</summary>
    public void RecordChronology()
    {
        var (entries, sha256) = Chronology();
        var record = new JsonObject
        {
            ["$comment"] = new JsonArray(Comment.Select(line => (JsonNode?)line).ToArray()),
            ["entries"] = entries,
            ["sha256"] = sha256,
        };

        Directory.CreateDirectory(FolderPath);
        File.WriteAllText(RecordPath, FixtureJson.Write(record, FixtureJson.Readable));
    }

    /// <summary>
    /// Writes every waiting entry after the chronology's last, removes its file, and records the
    /// chronology. It answers with the entries it wrote.
    /// <para>
    /// Every file is read before a line is written, and one refused entry stops the fold with the
    /// document and every file as they were. An entry whose first line the chronology already holds
    /// is refused too: that is what a fold run a second time over the same files would write.
    /// </para>
    /// </summary>
    public IReadOnlyList<LedgerEntry> Fold()
    {
        var waiting = Waiting();
        var (lines, from, to) = Read();
        var held = lines[from..to].ToHashSet(StringComparer.Ordinal);

        var refused = waiting
            .SelectMany(entry => entry.Problems.Select(problem => $"{entry.Name}: {problem}"))
            .Concat(waiting
                .Where(entry => held.Contains(entry.Lines[0]))
                .Select(entry => $"{entry.Name}: the chronology already holds an entry that opens as this one does"))
            .Concat(waiting
                .GroupBy(entry => entry.Lines[0], StringComparer.Ordinal)
                .Where(same => same.Count() > 1)
                .Select(same => $"{string.Join(" and ", same.Select(entry => entry.Name))}: they open the same way"))
            .ToList();
        if (refused.Count > 0)
            throw new InvalidOperationException(
                "the fold writes nothing while it refuses an entry:" + Environment.NewLine
                + string.Join(Environment.NewLine, refused));

        if (waiting.Count > 0)
        {
            var folded = lines[..to].Concat(waiting.SelectMany(entry => entry.Lines)).Concat(lines[to..]);
            File.WriteAllText(DocumentPath, string.Join("\n", folded));
            foreach (var entry in waiting)
                File.Delete(Path.Combine(FolderPath, entry.Name));
        }

        RecordChronology();
        return waiting;
    }

    /// <summary>The document's lines, and the chronology among them: from its heading to the last
    /// line that holds text before the next heading of its level. The empty lines that close the
    /// section are no part of it, so the fold writes above them and the digest ignores them.</summary>
    private (string[] Lines, int From, int To) Read()
    {
        var lines = File.ReadAllText(DocumentPath).ReplaceLineEndings("\n").Split('\n');
        var from = Array.IndexOf(lines, Heading);
        if (from < 0)
            throw new InvalidOperationException($"{Document} has no `{Heading}` heading, where its chronology starts");

        var next = Array.FindIndex(lines, from + 1, line => line.StartsWith("## ", StringComparison.Ordinal));
        var to = next < 0 ? lines.Length : next;
        while (to > from + 1 && lines[to - 1].Length == 0) to--;
        return (lines, from, to);
    }
}
