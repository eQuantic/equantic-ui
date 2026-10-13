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

    /// <summary>
    /// What a system writes into a folder somebody opened, which is no event: a Mac's and Windows'
    /// own, the two the repository's <c>.gitignore</c> names.
    /// <para>
    /// The list is by name and short on purpose. Skipping every file that starts with a dot would
    /// let an event saved as <c>.an-event.md</c> pass the guard and miss every fold, in silence. A
    /// file this does not name is read as an entry and refused by its name, which is loud, and a
    /// system file met later is one more name here.
    /// </para>
    /// </summary>
    private static readonly string[] LeftByASystem = [".DS_Store", "Thumbs.db"];

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

    private string DocumentPath => Path.Combine(root, Document);
    private string FolderPath => Path.Combine(root, Folder);
    private string RecordPath => Path.Combine(root, Record);

    /// <summary>
    /// The events waiting for a fold, in the order a fold writes them: by the day each states, then
    /// by name. Entries of one day have no order worth keeping, and a name gives the same one on
    /// every clone.
    /// <para>
    /// EVERY file of the folder is one, but its own page, the record, and the two files a system
    /// leaves behind (<see cref="LeftByASystem"/>). Reading only <c>*.md</c> would let an event
    /// saved as <c>.txt</c>, or as <c>.MD</c> on Linux, wait for ever with nothing saying so: it is
    /// read here, and its name is what refuses it. A hidden file is read the same way.
    /// </para>
    /// </summary>
    public IReadOnlyList<LedgerEntry> Waiting() =>
        Directory.Exists(FolderPath)
            ? Directory.GetFiles(FolderPath)
                .Where(path => Path.GetFileName(path) is var name
                    && name != Guide && name != Path.GetFileName(Record) && !LeftByASystem.Contains(name))
                .Select(LedgerEntry.Read)
                .OrderBy(entry => entry.Day)
                .ThenBy(entry => entry.Name, StringComparer.Ordinal)
                .ToList()
            : [];

    /// <summary>
    /// What keeps the folder from being folded as it is, and nothing when a fold would take all of
    /// it: a file that is not one entry, a folder inside it, two files that open the same way, and
    /// an entry whose first line the chronology already holds. The guard and the fold both ask
    /// here, so what a release would refuse fails in the pull request that wrote it.
    /// </summary>
    public IReadOnlyList<string> Refused()
    {
        var waiting = Waiting();
        var (lines, from, to) = Read();
        var held = lines[from..to].ToHashSet(StringComparer.Ordinal);

        return waiting
            .SelectMany(entry => entry.Problems.Select(problem => $"{Folder}/{entry.Name}: {problem}"))
            .Concat((Directory.Exists(FolderPath) ? Directory.GetDirectories(FolderPath) : [])
                .Select(folder => $"{Folder}/{Path.GetFileName(folder)}/: an event is a file of this folder, not of one inside it"))
            .Concat(waiting
                .Where(entry => held.Contains(entry.Lines[0]))
                .Select(entry => $"{Folder}/{entry.Name}: the chronology already holds an entry that opens as this "
                    + "one does. If a fold that stopped halfway wrote it, the file is what is left to remove"))
            .Concat(waiting
                .GroupBy(entry => entry.Lines[0], StringComparer.Ordinal)
                .Where(same => same.Count() > 1)
                .Select(same => $"{string.Join(" and ", same.Select(entry => $"{Folder}/{entry.Name}"))}: they open the same way"))
            .ToList();
    }

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
    /// Every file is read before a line is written, and one refusal stops the fold with the
    /// document and every file as they were (<see cref="Refused"/>). An entry whose first line the
    /// chronology already holds is one of them: that is what a fold run a second time over the same
    /// files would write.
    /// </para>
    /// </summary>
    public IReadOnlyList<LedgerEntry> Fold()
    {
        var refused = Refused();
        if (refused.Count > 0)
            throw new InvalidOperationException(
                "the fold writes nothing while it refuses an entry:" + Environment.NewLine
                + string.Join(Environment.NewLine, refused));

        var waiting = Waiting();
        var (lines, _, to) = Read();
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
