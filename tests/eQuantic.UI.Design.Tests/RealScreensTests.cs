using System.Text.RegularExpressions;
using eQuantic.UI.Design;
using FluentAssertions;

namespace eQuantic.UI.Design.Tests;

/// <summary>
/// The design host, asked about every node of every REAL screen in the dashboard sample.
/// <para>
/// This exists because of a bug the hand-written probes could not have found. They are declarative —
/// children pass through a named argument — and the fault only appeared where a construction is a
/// call's ONLY argument, which is how imperative code adds children and therefore how most of a real
/// screen is written. A probe inherits the assumptions of whoever wrote it; a real screen does not.
/// </para>
/// <para>
/// The check is a CROSS-EXAMINATION, not a smoke test. The compiler stamps each node with a label at
/// emission time, and the host resolves the component from the span afterwards. Two independent
/// paths to the same answer: when they disagree, one of them is wrong, and neither can hide it.
/// </para>
/// </summary>
public class RealScreensTests
{
    private static string? RepoRoot()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "src", "eQuantic.UI.Runtime")))
            here = here.Parent;
        return here?.FullName;
    }

    /// <summary>
    /// The sample as an ordinary build leaves it. Absent on a fresh clone, so this returns rather
    /// than fails — but it means the sweep only bites where the sample has been built, which is the
    /// machine of whoever is working on the host. Saying so here beats a green tick that checked
    /// nothing.
    /// </summary>
    private static (string Dir, string Refs, string Generated)? Sample()
    {
        var root = RepoRoot();
        if (root is null) return null;

        var dir = Path.Combine(root, "samples", "DefaultUIDashboard");
        var refs = Path.Combine(dir, "obj", "Debug", "net10.0", "equantic.refs.txt");
        return File.Exists(refs) && new FileInfo(refs).Length > 0
            ? (dir, refs, Path.Combine(dir, "obj", "Debug", "net10.0", "generated"))
            : null;
    }

    /// <summary>`path|line:col|line:col`, then the label the compiler stamped beside it.</summary>
    private static readonly Regex Stamp = new(@"""([^""]+\|\d+:\d+\|\d+:\d+)"", ""([^""]+)""", RegexOptions.Compiled);

    [Fact]
    public void EveryNodeOfEveryScreen_ResolvesToTheComponentThatWasStamped()
    {
        if (Sample() is not var (dir, refs, generated)) return;

        var session = new DesignSession();
        session.Initialize(dir, refs, generated);

        var screens = Directory.GetFiles(Path.Combine(dir, "Screens"), "*.cs");
        screens.Should().NotBeEmpty();

        var disagreements = new List<string>();
        var checkedNodes = 0;
        var screensWithNodes = 0;

        foreach (var screen in screens)
        {
            var text = File.ReadAllText(screen);
            var compiled = session.Compile(screen, text);
            if (!compiled.Success) continue;

            var stamps = Stamp.Matches(compiled.Js)
                .Select(m => (Origin: m.Groups[1].Value, Label: m.Groups[2].Value))
                .Where(s => s.Origin.StartsWith(screen, StringComparison.OrdinalIgnoreCase))
                .DistinctBy(s => s.Origin)
                .ToArray();

            if (stamps.Length > 0) screensWithNodes++;

            foreach (var (origin, label) in stamps)
            {
                checkedNodes++;
                var node = session.Inspect(screen, text, origin);
                var where = $"{Path.GetFileName(screen)} {origin.Split('|')[1]} ({label})";

                if (node is null)
                {
                    disagreements.Add($"{where}: the host resolved nothing at all");
                    continue;
                }

                // `Void` is the signature of the tie-break bug: the span matched an argument, the
                // walk up found the CALL that takes it, and a call that adds a child returns void.
                if (node.Component == "Void")
                {
                    disagreements.Add($"{where}: resolved to Void — the span matched a call, not the node");
                    continue;
                }

                // A label of the form `Method()` names a helper whose static return type really is
                // the abstract node, so only the concrete ones are compared.
                var expected = label.Split(" · ")[0];
                if (!expected.EndsWith("()", StringComparison.Ordinal) && node.Component != expected)
                {
                    disagreements.Add($"{where}: the compiler stamped {expected}, the host resolved {node.Component}");
                }
            }
        }

        checkedNodes.Should().BeGreaterThan(200,
            "the sweep is only worth having if it covers the real screens, not a handful of nodes");
        screensWithNodes.Should().BeGreaterThan(4);

        disagreements.Should().BeEmpty(
            "the compiler's label and the host's resolution are two independent answers to the same "
            + "question, and they have to agree:\n" + string.Join("\n", disagreements.Take(25)));
    }
}
