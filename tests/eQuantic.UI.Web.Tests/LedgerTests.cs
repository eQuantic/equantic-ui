using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// An event of the ledger is a file of its own under <c>docs/ledger/</c>, and a release folds the
/// files into the chronology of <c>docs/LEDGER.md</c> (#892).
/// <para>
/// Every pull request used to add its entry at one point, the chronology's end, so any two open pull
/// requests conflicted there and each merge into main left the others with a conflict to resolve by
/// hand. Committing <c>merge=union</c> for the file does not help: a merge GitHub performs reads no
/// attribute a tree commits. Measured through its API between scratch branches, a file marked
/// <c>merge=union</c> conflicted where git merges it, and a text file under <c>*.metal binary</c>
/// merged where git refuses it. A file nobody else adds is the one point nobody else writes.
/// </para>
/// <para>
/// Two things can go wrong in silence, and each has a case here. A file that is not an entry would
/// be folded into the chronology as whatever it holds. And an entry written at the chronology's end
/// by habit still works, and brings the conflict back: so <c>docs/ledger/folded.json</c> records the
/// chronology as the last fold left it, and the comparison fails when it differs.
/// </para>
/// <para>
/// <c>EQ_FOLD_LEDGER=1</c> folds what waits and records, which is a release's step
/// (<c>./scripts/fold-ledger.sh</c>). <c>EQ_UPDATE_LEDGER=1</c> records and folds nothing: it is for
/// a deliberate change to the chronology itself, a citation that moved with its document or a
/// retired plan's lines. It is not for an entry written there by hand, which is a file.
/// </para>
/// </summary>
public class LedgerTests
{
    /// <summary>A ledger of twelve lines. Its line endings are made LF here, whatever the checkout
    /// gave this file, since every case below writes and compares with LF.</summary>
    private static readonly string Chronology =
        """
        # The ledger

        What happened, when, and where the record is.

        ## Chronology

        ### 2026-09 — a month

        - **2026-09-29 · The first event**: what happened, and what was measured
          ([#1](https://github.com/eQuantic/equantic-ui/issues/1)).
        - **An event from before entries were dated** (#2).

        ## Retired documents

        | Document | Where it went |
        |---|---|

        """.ReplaceLineEndings("\n");

    private static readonly string Entry =
        """
        - **2026-10-13 · A class named as .NET names one of its types is a module the page
          imports**: a library's `Type` got no module
          ([#888](https://github.com/eQuantic/equantic-ui/issues/888)).

        """.ReplaceLineEndings("\n");

    [Fact]
    public void EveryEventWaitingForAFold_IsOneDatedEntryThatCitesItsEvidence()
    {
        var root = SdkProbe.RepoRoot();

        File.Exists(Path.Combine(root, Ledger.Folder, Ledger.Guide)).Should().BeTrue(
            $"{Ledger.Folder}/{Ledger.Guide} says what a file beside it is, and keeps the folder there after a fold");

        // Joined, so the failure names every one of them rather than the first.
        string.Join(Environment.NewLine, new Ledger(root).Refused()).Should().BeEmpty(
            "a file here is folded into the chronology as it is, so it holds one event as the chronology "
            + "holds one: a list item that opens with its day, continues indented, and cites its evidence. "
            + "What a release's fold would refuse fails here, in the pull request that wrote it");
    }

    [Fact]
    public void AnEntryAsTheChronologyHoldsOne_IsAccepted()
    {
        new LedgerEntry("a-class-named-type-is-a-module.md", Entry).Problems.Should().BeEmpty();
        new LedgerEntry("0.2.0-preview.63-released.md", "- **2026-10-20 · 0.2.0-preview.63 released** from `1b8eca6a` (#900).\n")
            .Problems.Should().BeEmpty("a release is an event like another, and its file is named by its version");
        new LedgerEntry("an-entry.md", "- **2026-10-13 · An event**: its text wraps where it wraps, on line\n  22) and after it, and\n  **bold** text or a -1 may open a line (#1).\n")
            .Problems.Should().BeEmpty("a line that starts with a number, with bold text or with a sign opens no list item");
    }

    /// <summary>Each shape the fold would write into the chronology as something that is not one
    /// entry, and the one thing each lacks.</summary>
    [Theory]
    [InlineData("An-Entry.md", "- **2026-10-13 · An event**: what happened (#1).\n", "its name")]
    [InlineData("an entry.md", "- **2026-10-13 · An event**: what happened (#1).\n", "its name")]
    [InlineData("an-entry.txt", "- **2026-10-13 · An event**: what happened (#1).\n", "its name")]
    [InlineData("an-entry.md", "- **An event with no day**: what happened (#1).\n", "its first line")]
    [InlineData("an-entry.md", "- **2026-02-30 · A day the calendar does not hold**: what happened (#1).\n", "its first line")]
    [InlineData("an-entry.md", "**2026-10-13 · No list item**: what happened (#1).\n", "its first line")]
    [InlineData("an-entry.md", "- **2026-10-13 · A title that never closes: what happened (#1).\n", "never closes")]
    [InlineData("an-entry.md", "- **2026-10-13 · A title that never closes: what happened to **one** word (#1).\n", "never closes")]
    [InlineData("an-entry.md", "- **2026-10-13 · An event**: what happened (#1).\n- **2026-10-13 · A second**: more (#2).\n", "line 2")]
    [InlineData("an-entry.md", "- **2026-10-13 · An event**: what happened\nat the margin (#1).\n", "line 2")]
    [InlineData("an-entry.md", "- **2026-10-13 · An event**: what happened\n\n  after an empty line (#1).\n", "line 2")]
    [InlineData("an-entry.md", "- **2026-10-13 · An event**: what happened\n  and more (#1).\n\n## A heading\n", "line 3")]
    [InlineData("an-entry.md", "- **2026-10-13 · An event**: what happened (#1).\n  - **2026-10-13 · A second, typed below it**: more (#2).\n", "line 2 opens a list item")]
    [InlineData("an-entry.md", "- **2026-10-13 · An event**: what happened\n  and more,\n  * with an item under it (#1).\n", "line 3 opens a list item")]
    [InlineData("an-entry.md", "- **2026-10-13 · An event**: what happened\n  + with an item under it (#1).\n", "line 2 opens a list item")]
    [InlineData("an-entry.md", "- **2026-10-13 · An event**: what happened, and no link.\n", "cites no")]
    public void AFileThatIsNotOneDatedEvent_IsRefused_ForWhatItLacks(string name, string text, string lacks)
    {
        new LedgerEntry(name, text).Problems
            .Should().ContainSingle("one thing is wrong with it")
            .Which.Should().Contain(lacks);
    }

    [Fact]
    public void AnEmptyFile_IsRefused()
    {
        new LedgerEntry("an-entry.md", string.Empty).Problems.Should().NotBeEmpty();
    }

    /// <summary>Two asterisks inside code are a glob, and open no bold text.</summary>
    [Fact]
    public void AGlobInCode_OpensNoBoldText()
    {
        new LedgerEntry("an-entry.md", "- **2026-10-13 · An event**: it reads `**/PublicAPI.Unshipped.txt` (#1).\n")
            .Problems.Should().BeEmpty();
    }

    /// <summary>
    /// Whatever the folder holds is read, so nothing waits there for ever. A scan of <c>*.md</c>
    /// alone let an event saved under another extension, or in a folder of its own, pass this guard
    /// and miss every fold, with nothing saying so. Skipping every hidden file did the same for an
    /// event saved with a dot first: what is skipped is two files a system writes, by name.
    /// </summary>
    [Fact]
    public void AFileOfAnotherKind_OrAFolder_IsRefused_RatherThanLeftWaiting()
    {
        var root = Tree(Chronology,
            ("an-event.txt", "- **2026-10-03 · An event**: saved as text (#20).\n"),
            ("AN-EVENT.MD", "- **2026-10-03 · Another event**: saved in capitals (#21).\n"),
            (".an-event.md", "- **2026-10-03 · A hidden event**: saved with a dot first (#22).\n"),
            (".DS_Store", "what a Mac leaves in a folder somebody opened"),
            ("Thumbs.db", "what Windows leaves in a folder somebody opened"),
            (Ledger.Guide, "# Events waiting for a fold\n"));
        Directory.CreateDirectory(Path.Combine(root, Ledger.Folder, "2026"));
        try
        {
            var ledger = new Ledger(root);

            ledger.Refused().Should().BeEquivalentTo(new[]
            {
                "docs/ledger/an-event.txt: its name is lower-case words joined by hyphens, ending in .md",
                "docs/ledger/AN-EVENT.MD: its name is lower-case words joined by hyphens, ending in .md",
                "docs/ledger/.an-event.md: its name is lower-case words joined by hyphens, ending in .md",
                "docs/ledger/2026/: an event is a file of this folder, not of one inside it",
            });
            var fold = () => ledger.Fold();
            fold.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("docs/ledger/2026/");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// The comparison that stops the old habit. An entry written at the chronology's end still
    /// reads well and still conflicts with every other pull request that did the same, and nothing
    /// else would say so.
    /// </summary>
    [Fact]
    public void TheChronologyIsWhatTheLastFoldLeft()
    {
        var ledger = new Ledger(SdkProbe.RepoRoot());

        if (Environment.GetEnvironmentVariable("EQ_FOLD_LEDGER") == "1")
        {
            ledger.Fold();
            return;
        }

        if (Environment.GetEnvironmentVariable("EQ_UPDATE_LEDGER") == "1")
        {
            ledger.RecordChronology();
            return;
        }

        ledger.Recorded().Should().NotBeNull($"{Ledger.Record} records the chronology as the last fold left it");
        var left = ledger.Recorded().GetValueOrDefault();

        var now = ledger.Chronology();
        var differs = now.Entries > left.Entries
            ? $"it holds {now.Entries} entries where the last fold left {left.Entries}. An event is a file of "
                + $"its own under {Ledger.Folder}/ (its {Ledger.Guide} says how), so that two pull requests never "
                + "write at the same point: move the entry there"
            : $"its text changed since the last fold, which left {left.Entries} entries";
        now.Should().Be(left,
            $"the chronology of {Ledger.Document} is written by a release's fold, and {differs}. A deliberate "
            + "change to the chronology itself, a citation that moved with its document, is recorded with "
            + "EQ_UPDATE_LEDGER=1 on this test, which folds nothing");
    }

    [Fact]
    public void AFoldWritesEveryWaitingEntryAfterTheChronologysLast_ByDayThenByName_AndRemovesItsFile()
    {
        var root = Tree(Chronology,
            ("b-second-of-its-day.md", "- **2026-10-02 · B**: second of its day (#12).\n"),
            ("a-first-of-its-day.md", "- **2026-10-02 · A**: first of its day,\n  on two lines (#11).\n"),
            ("c-the-day-before.md", "- **2026-10-01 · C**: the day before (#13).\n"),
            (Ledger.Guide, "# Events waiting for a fold\n"));
        try
        {
            var ledger = new Ledger(root);

            ledger.Fold().Select(entry => entry.Name).Should().Equal(
                "c-the-day-before.md", "a-first-of-its-day.md", "b-second-of-its-day.md");

            File.ReadAllText(Path.Combine(root, Ledger.Document)).Should().Be(Chronology.Replace(
                "- **An event from before entries were dated** (#2).\n",
                "- **An event from before entries were dated** (#2).\n"
                + "- **2026-10-01 · C**: the day before (#13).\n"
                + "- **2026-10-02 · A**: first of its day,\n  on two lines (#11).\n"
                + "- **2026-10-02 · B**: second of its day (#12).\n"),
                "the entries follow the chronology's last, and every other line is as it was");
            Directory.GetFiles(Path.Combine(root, Ledger.Folder)).Select(Path.GetFileName)
                .Should().BeEquivalentTo(new[] { Ledger.Guide, Path.GetFileName(Ledger.Record) },
                    "an entry the chronology holds is no longer a file, and the folder's own page stays");
            ledger.Recorded().Should().Be(ledger.Chronology(), "the fold records what it left");
            ledger.Chronology().Entries.Should().Be(5);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AFoldWithNothingWaiting_LeavesTheDocumentByteForByte()
    {
        var root = Tree(Chronology, (Ledger.Guide, "# Events waiting for a fold\n"));
        try
        {
            var document = Path.Combine(root, Ledger.Document);
            var before = File.ReadAllBytes(document);

            new Ledger(root).Fold().Should().BeEmpty();

            File.ReadAllBytes(document).Should().Equal(before);
            new Ledger(root).Recorded().Should().Be(new Ledger(root).Chronology());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// Every file is read before a line is written. A fold that wrote the good entries and stopped
    /// at the bad one would leave a chronology no record matches and files half removed.
    /// </summary>
    [Theory]
    [InlineData("- **An event with no day**: what happened (#21).\n", "not-an-entry.md: its first line")]
    [InlineData("- **2026-09-29 · The first event**: what happened, and what was measured\n  again (#1).\n", "already holds")]
    [InlineData("- **2026-10-03 · A good one**: it is a file already (#20).\n", "they open the same way")]
    public void AFoldThatRefusesOneEntry_WritesNothing(string refusedText, string says)
    {
        var root = Tree(Chronology,
            ("a-good-one.md", "- **2026-10-03 · A good one**: it is a file already (#20).\n"),
            ("not-an-entry.md", refusedText));
        try
        {
            var document = Path.Combine(root, Ledger.Document);
            var before = File.ReadAllBytes(document);

            var fold = () => new Ledger(root).Fold();

            fold.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain(says);
            File.ReadAllBytes(document).Should().Equal(before);
            Directory.GetFiles(Path.Combine(root, Ledger.Folder)).Select(Path.GetFileName)
                .Should().BeEquivalentTo(new[] { "a-good-one.md", "not-an-entry.md" }, "no file went, and no record was written");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>What the comparison sees and what it does not: the chronology's own lines, and
    /// nothing above its heading, below its last entry or after the next heading.</summary>
    [Fact]
    public void AnEntryWrittenIntoTheChronologyByHand_IsNotWhatTheFoldLeft()
    {
        var root = Tree(Chronology);
        try
        {
            var ledger = new Ledger(root);
            var document = Path.Combine(root, Ledger.Document);
            ledger.RecordChronology();
            var left = ledger.Recorded().GetValueOrDefault();

            File.WriteAllText(document, Chronology
                .Replace("What happened, when, and where the record is.", "What happened, when, and where the record is, reworded.")
                .Replace("(#2).\n\n## Retired", "(#2).\n\n\n\n## Retired")
                .Replace("| Document | Where it went |", "| Document | Where it went | When |"));
            ledger.Chronology().Should().Be(left, "the page around the chronology is not the fold's to write");

            File.WriteAllText(document, Chronology.Replace(
                "(#2).\n", "(#2).\n- **2026-10-13 · An entry written here by habit**: it reads well (#30).\n"));
            ledger.Chronology().Should().NotBe(left);
            ledger.Chronology().Entries.Should().Be(left.Entries + 1);

            File.WriteAllText(document, Chronology.Replace("what happened, and what was measured", "what happened"));
            ledger.Chronology().Should().NotBe(left, "an entry reworded is a change to the chronology too");
            ledger.Chronology().Entries.Should().Be(left.Entries);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// The fold, run over THIS tree's ledger and the entries waiting in it, on a copy. The cases
    /// above write a chronology of twelve lines. The real one has thousands, four headings and
    /// entries from before a day opened each one, and it is the one a release folds.
    /// </summary>
    [Fact]
    public void AFoldOfThisTreesLedger_AddsWhatWaits_AndKeepsEveryOtherLine()
    {
        var source = SdkProbe.RepoRoot();
        var waiting = new Ledger(source).Waiting();
        var root = Tree(
            File.ReadAllText(Path.Combine(source, Ledger.Document)),
            [.. waiting.Select(entry => (entry.Name, entry.Text))]);
        try
        {
            var ledger = new Ledger(root);
            var before = File.ReadAllText(Path.Combine(root, Ledger.Document));
            var entries = ledger.Chronology().Entries;

            ledger.Fold().Should().HaveCount(waiting.Count);

            var after = File.ReadAllText(Path.Combine(root, Ledger.Document));
            var written = string.Concat(waiting.Select(entry => string.Join("\n", entry.Lines) + "\n"));
            after.Length.Should().Be(before.Length + written.Length);
            if (written.Length > 0)
            {
                var at = after.IndexOf(written, StringComparison.Ordinal);
                at.Should().BePositive("the entries are written together, in the order they wait");
                after.Remove(at, written.Length).Should().Be(before, "every other line is as it was");
                after[(at + written.Length)..].Should().StartWith("\n## ", "they are the chronology's last");
            }

            ledger.Chronology().Entries.Should().Be(entries + waiting.Count);
            ledger.Waiting().Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>A tree that holds a ledger and nothing else: the document, and a file for each event
    /// waiting.</summary>
    private static string Tree(string document, params (string Name, string Text)[] waiting)
    {
        var root = Directory.CreateTempSubdirectory("eq-ledger-").FullName;
        Directory.CreateDirectory(Path.Combine(root, Ledger.Folder));
        File.WriteAllText(Path.Combine(root, Ledger.Document), document.ReplaceLineEndings("\n"));
        foreach (var (name, text) in waiting)
            File.WriteAllText(Path.Combine(root, Ledger.Folder, name), text);
        return root;
    }
}
