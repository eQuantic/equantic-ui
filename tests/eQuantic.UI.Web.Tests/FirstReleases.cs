using System.Buffers;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The first release whose files hold each of a set of searches, which is what a version mark claims
/// (#999).
/// <para>
/// Each release is compared with the one before it, the first with the empty tree, and every search
/// is counted in the same pass over what changed: a search's count in a release is its count in the
/// release before plus what the diff between them added, less what it removed, and the first release
/// where the count is above zero is the release the search shipped in. One diff per release answers
/// every mark at once.
/// </para>
/// <para>
/// The guard used to ask git once per mark, a pickaxe search over every ref, and on CI every ref is a
/// commit fetched at depth one, a root whose diff is its whole tree, so each search read about three
/// hundred trees: 159 marks took 34 to 39 minutes on CI's runners, and then more than the hour CI
/// gives a case. Release to release, CI's shallow checkout and a full clone read the same trees and
/// give the same answers. Measured against the pickaxe searches, every answer matched on CI's checkout,
/// and on a full clone all but one: a name added and renamed on the same day, whose commits a release
/// held and whose text no release tree ever did, which this answers as never shipped.
/// </para>
/// </summary>
internal sealed class FirstReleases
{
    /// <summary>The empty tree, the base the first release is compared with. git knows its id whether
    /// or not a repository holds it, one for each object format.</summary>
    private const string Sha1EmptyTree = "4b825dc642cb6eb9a060e54bf8d69288fbee4904";

    private const string Sha256EmptyTree = "6ef19b41225c5369f1c104d45d8d85efa9b057b53b14b4b9b939dd74decc5321";

    /// <summary>
    /// Each git call's deadline. The longest is the first release's diff, its whole tree, read in
    /// about three seconds on a laptop. A git that hangs fails the case rather than holding the
    /// test host (#971), and well inside the fifteen minutes CI gives a case.
    /// </summary>
    private static readonly TimeSpan Deadline = TimeSpan.FromMinutes(3);

    private readonly Dictionary<string, string> _releases;

    private FirstReleases(Dictionary<string, string> releases) => _releases = releases;

    /// <summary>The release <paramref name="search"/> first shipped in, without the tag's leading
    /// <c>v</c>, or null when no release holds it yet: a mark on unreleased work is a promise, and the
    /// guard is about what is true.</summary>
    public string? Of(string search) => _releases.GetValueOrDefault(search);

    /// <summary>
    /// Reads the release tags of the repository at <paramref name="root"/> and answers, for each of
    /// <paramref name="searches"/>, the first one whose files hold it. A search is a text, or
    /// <c>text @ path</c>, which counts the text only in the file at that path or under that
    /// directory: a member name is rarely unique across a framework that has both a Core and a
    /// Primitives component model, and <c>OnMount</c> matched the older one. The path is a path, not
    /// a pattern.
    /// </summary>
    public static FirstReleases Read(string root, IEnumerable<string> searches)
    {
        var wanted = searches.Distinct(StringComparer.Ordinal).Select(Search.Parse).ToArray();
        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        if (wanted.Length == 0) return new FirstReleases(found);

        var texts = SearchValues.Create(
            [.. wanted.Select(search => search.Text).Distinct(StringComparer.Ordinal)], StringComparison.Ordinal);
        var counts = new long[wanted.Length];
        var before = Git(root, "rev-parse", "--show-object-format").Trim() == "sha256" ? Sha256EmptyTree : Sha1EmptyTree;
        foreach (var release in Releases(root))
        {
            // No renames: a file that moved is the file it left and the file it became, so its count
            // leaves the path it had and joins the one it has. No prefixes, no colour, no external
            // diff and no text conversion, whatever the configuration of the machine says.
            Count(Git(root,
                    "-c", "core.quotePath=false", "diff", "--no-renames", "--no-prefix", "--no-color",
                    "--no-ext-diff", "--no-textconv", "--unified=0", before, $"refs/tags/{release}"),
                wanted, texts, counts);
            for (var i = 0; i < wanted.Length; i++)
                if (counts[i] > 0) found.TryAdd(wanted[i].Written, release[1..]);
            before = $"refs/tags/{release}";
        }

        return new FirstReleases(found);
    }

    /// <summary>The release tags, oldest first.</summary>
    private static IEnumerable<string> Releases(string root) =>
        Git(root, "tag", "--list", "v0.*")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(tag => (Tag: tag, Order: PreviewOrder(tag)))
            .Where(entry => entry.Order >= 0)
            .OrderBy(entry => entry.Order)
            .Select(entry => entry.Tag);

    /// <summary>Sorts preview.9 BEFORE preview.10: the whole point of the check is off-by-one
    /// version numbers, and a lexical sort has its own.</summary>
    private static int PreviewOrder(string tag) =>
        Regex.Match(tag, @"preview\.(\d+)$") is { Success: true } match
            ? int.Parse(match.Groups[1].Value)
            : -1;

    /// <summary>
    /// Adds what one diff changes to each search's count. A line opening with <c>---</c> or
    /// <c>+++</c> names a file only in a file's header, before its first hunk: inside a hunk it is a
    /// removed line that began <c>--</c> or an added one that began <c>++</c>.
    /// </summary>
    private static void Count(string diff, Search[] wanted, SearchValues<string> texts, long[] counts)
    {
        string? from = null, to = null;
        var inHunk = false;
        var rest = diff.AsSpan();
        while (!rest.IsEmpty)
        {
            // Lines end at a line feed and nowhere else: a carriage return, a form feed or a Unicode
            // separator inside a line is part of it, and must not start a line of its own.
            var end = rest.IndexOf('\n');
            var line = end < 0 ? rest : rest[..end];
            rest = end < 0 ? ReadOnlySpan<char>.Empty : rest[(end + 1)..];

            if (line.StartsWith("diff --git ", StringComparison.Ordinal))
            {
                (from, to, inHunk) = (null, null, false);
                continue;
            }
            if (!inHunk)
            {
                if (line.StartsWith("--- ", StringComparison.Ordinal)) from = PathOf(line[4..]);
                else if (line.StartsWith("+++ ", StringComparison.Ordinal)) to = PathOf(line[4..]);
                else if (line.StartsWith("@@", StringComparison.Ordinal)) inHunk = true;
                continue;
            }

            // A hunk's own header and "\ No newline at end of file" change no count.
            var sign = line.IsEmpty ? 0 : line[0] switch { '+' => 1, '-' => -1, _ => 0 };
            if (sign == 0) continue;
            var text = line[1..];
            if (text.IndexOfAny(texts) < 0) continue;
            var path = sign > 0 ? to : from;
            for (var i = 0; i < wanted.Length; i++)
                if (wanted[i].Covers(path)) counts[i] += sign * Occurrences(text, wanted[i].Text);
        }
    }

    /// <summary>How many times <paramref name="text"/> occurs in <paramref name="line"/>, none
    /// overlapping, as git's pickaxe counts.</summary>
    private static int Occurrences(ReadOnlySpan<char> line, string text)
    {
        var count = 0;
        for (var at = line.IndexOf(text, StringComparison.Ordinal); at >= 0; at = line.IndexOf(text, StringComparison.Ordinal))
        {
            count++;
            line = line[(at + text.Length)..];
        }
        return count;
    }

    /// <summary>
    /// The path a file header names, or null for <c>/dev/null</c>, the side of a file that was added
    /// or deleted. git ends a name that holds a space with a tab, and writes a name that holds a
    /// quote, a backslash or a control character between quotes, escaping that character: the quotes
    /// go and the escape stays, since a search's path is compared by its start, and the paths marks
    /// name hold none of those characters.
    /// </summary>
    private static string? PathOf(ReadOnlySpan<char> header)
    {
        header = header.TrimEnd('\t');
        if (header.SequenceEqual("/dev/null")) return null;
        return (header is ['"', .. var quoted, '"'] ? quoted : header).ToString();
    }

    private static string Git(string root, params string[] arguments)
    {
        var (exitCode, output, error) = ChildProcess.Run("git", arguments, Deadline, root);
        exitCode.Should().Be(0, $"git {string.Join(' ', arguments)} must answer: {error}");
        return output;
    }

    /// <summary>A search as a mark writes it, the text it counts, and the path it counts under when
    /// it names one.</summary>
    private readonly record struct Search(string Written, string Text, string? Scope)
    {
        public static Search Parse(string written) =>
            written.Split(" @ ") is [var text, var scope]
                ? new Search(written, text, scope.TrimEnd('/'))
                : new Search(written, written, null);

        /// <summary>Whether a change to <paramref name="path"/> counts: the file the scope names, or
        /// one under the directory it names.</summary>
        public bool Covers(string? path) =>
            Scope is null
            || (path is not null
                && path.StartsWith(Scope, StringComparison.Ordinal)
                && (path.Length == Scope.Length || path[Scope.Length] == '/'));
    }
}
