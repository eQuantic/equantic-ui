using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What ships and what is still a REQUEST, held to the SDK instead of typed by hand.
///
/// <para>
/// The pages carried status as prose and badges, and prose does not notice a release. When this was
/// written, DatePicker, NavigationRail, VariantColors.Hover and the SemanticNode state fields had all
/// shipped while four pages still called them requests, and every footer said "C3 · C15 · C16 ahead
/// of the SDK" after two of the three had landed.
/// </para>
///
/// <para>
/// <c>docs/design/status.json</c> lists every item that is, or was, a request, with a PROBE that
/// proves its state: a line of a PublicAPI file, or a reference in the source. Each item is checked
/// both ways. A request whose probe appears has shipped, and fails until the entry and the pages
/// move; a shipped item whose probe vanished fails too. A shipped item's CLAIMS, the phrases only a
/// request may carry, must be gone from every page, so a re-export cannot bring a stale badge back.
/// </para>
/// </summary>
public class HandoffStatusTests
{
    private static readonly string Root = RepositoryRoot();

    private static readonly JsonElement Status = JsonDocument.Parse(
        File.ReadAllText(Path.Combine(Root, "docs", "design", "status.json"))).RootElement;

    private static readonly string[] Statuses = ["shipped", "partial", "request"];

    [Fact]
    public void EachEntryMatchesWhatTheSdkShips()
    {
        var offences = new List<string>();
        foreach (var item in Status.GetProperty("items").EnumerateArray())
        {
            var id = item.GetProperty("id").GetString()!;
            var status = item.GetProperty("status").GetString()!;
            if (!Statuses.Contains(status))
            {
                offences.Add($"{id}: \"{status}\" is not one of {string.Join(", ", Statuses)}");
                continue;
            }

            // An item the SDK cannot prove is checked by hand, and says WHY in "checkedByHand". A null
            // probe used to be skipped with no reason at all, and that is how the APCA palette stayed a
            // request in this file after the pull request that shipped it (found in review).
            if (!item.TryGetProperty("probe", out var probe) || probe.ValueKind == JsonValueKind.Null)
            {
                if (!item.TryGetProperty("checkedByHand", out var why) || string.IsNullOrWhiteSpace(why.GetString()))
                    offences.Add($"{id} has no probe and does not say why: give it one, or a \"checkedByHand\" reason");
                continue;
            }

            var found = Probe(probe);
            if (status == "request" && found.All)
                offences.Add($"{id} has shipped ({found.What}). Mark it shipped in status.json and take its claims off the pages");
            else if (status == "request" && found.Any)
                offences.Add($"{id} has partly shipped ({found.What}, not {found.Missing}). Mark it partial");
            else if (status == "shipped" && !found.All)
                offences.Add($"{id} is marked shipped, and {found.Missing} is not in the SDK");
            else if (status == "partial" && found.All)
                offences.Add($"{id} has shipped the rest ({found.What}). Mark it shipped and take its claims off the pages");
            else if (status == "partial" && !found.Any)
                offences.Add($"{id} is marked partial, and none of {found.Missing} is in the SDK");
        }

        offences.Should().BeEmpty("status.json has to say what the SDK actually ships:" + List(offences));
    }

    [Fact]
    public void NoPageCallsAShippedItemARequest()
    {
        var shipped = Status.GetProperty("items").EnumerateArray()
            .Where(item => item.GetProperty("status").GetString() == "shipped")
            .Select(item => (Id: item.GetProperty("id").GetString()!, Claims: Claims(item)))
            .ToArray();

        var offences = new List<string>();
        foreach (var file in HandoffFiles())
        {
            var lines = File.ReadAllLines(file);
            var relative = Path.GetRelativePath(Root, file);
            for (var i = 0; i < lines.Length; i++)
                foreach (var (id, claims) in shipped)
                    foreach (var claim in claims.Where(c => lines[i].Contains(c, StringComparison.Ordinal)))
                        offences.Add($"{relative}:{i + 1}: \"{claim}\" ({id} has shipped)");
        }

        offences.Should().BeEmpty(
            "a page still describes a shipped item as a request. Correct the page here and let it flow back "
            + "to the design tool:" + List(offences));
    }

    // ---- probes ---------------------------------------------------------------------------------

    /// <summary>What a probe found: whether EVERY part it names is in the SDK, whether ANY is, what
    /// was found and what was not.</summary>
    private readonly record struct Found(bool All, bool Any, string What, string Missing)
    {
        public static Found Single(bool present, string what) => new(present, present, what, what);
    }

    private static Found Probe(JsonElement probe)
    {
        // One entry, or a LIST of them when the item promises several members: the promise ships
        // when every one does, and a list of which only some are in the SDK is partial. A single
        // probe for "Selected / Checked / Expanded" kept the item shipped with two of the three gone.
        if (probe.TryGetProperty("api", out var api))
        {
            string[] names = api.ValueKind == JsonValueKind.Array
                ? [.. api.EnumerateArray().Select(name => name.GetString()!)]
                : [api.GetString()!];
            var lines = ApiLines(probe).ToArray();
            var present = names.Where(name =>
            {
                var entry = new Regex(@"(^|[\s!?(,])" + Regex.Escape(name) + @"($|[.\s(<!?,])");
                return lines.Any(line => entry.IsMatch(line));
            }).ToArray();
            var missing = names.Except(present, StringComparer.Ordinal).ToArray();
            return new Found(missing.Length == 0, present.Length > 0,
                Entries(present.Length > 0 ? present : names), Entries(missing));
        }

        if (probe.TryGetProperty("apiContains", out var fragment))
        {
            var text = fragment.GetString()!;
            return Found.Single(ApiLines(probe).Any(line => line.Contains(text, StringComparison.Ordinal)),
                $"a public API entry containing {text}");
        }

        if (probe.TryGetProperty("sourceReference", out var symbol))
        {
            var name = symbol.GetString()!;
            var except = probe.TryGetProperty("except", out var list)
                ? list.EnumerateArray().Select(e => Normalize(e.GetString()!)).ToHashSet(StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);
            var word = new Regex(@"\b" + Regex.Escape(name) + @"\b");
            var hit = Directory.EnumerateFiles(Path.Combine(Root, "src"), "*.cs", SearchOption.AllDirectories)
                .Select(f => Normalize(Path.GetRelativePath(Root, f)))
                .Where(f => !IsBuildOutput(f) && !except.Contains(f))
                .OrderBy(f => f, StringComparer.Ordinal)
                .FirstOrDefault(f => word.IsMatch(File.ReadAllText(Path.Combine(Root, f))));
            return hit is null
                ? Found.Single(false, $"a reference to {name} in src outside {string.Join(", ", except)}")
                : Found.Single(true, $"{name} is referenced in {hit}");
        }

        throw new InvalidOperationException("a probe names api, apiContains or sourceReference");
    }

    private static string Entries(IReadOnlyList<string> names) =>
        names.Count == 1 ? $"the public API entry {names[0]}" : $"the public API entries {string.Join(", ", names)}";

    /// <summary>The public API files of the assembly the probe names, which list every public member
    /// by its full name. Missing files are an error in status.json, not an absence.</summary>
    private static IEnumerable<string> ApiLines(JsonElement probe)
    {
        var assembly = probe.GetProperty("in").GetString()!;
        var files = new[] { "PublicAPI.Shipped.txt", "PublicAPI.Unshipped.txt" }
            .Select(f => Path.Combine(Root, "src", assembly, f))
            .Where(File.Exists)
            .ToArray();
        if (files.Length == 0)
            throw new InvalidOperationException($"status.json names {assembly}, which has no PublicAPI file under src/");
        return files.SelectMany(f => File.ReadLines(f));
    }

    private static string[] Claims(JsonElement item) =>
        item.TryGetProperty("claims", out var claims)
            ? claims.EnumerateArray().Select(c => c.GetString()!).ToArray()
            : [];

    /// <summary>Pages, the token export and the notes. Not <c>frames/</c>, and not status.json, which
    /// carries every claim on purpose.</summary>
    private static IEnumerable<string> HandoffFiles() =>
        Directory.EnumerateFiles(Path.Combine(Root, "docs", "design"), "*", SearchOption.AllDirectories)
            .Where(f => Path.GetExtension(f) is ".html" or ".json" or ".md")
            .Where(f => !Path.GetRelativePath(Root, f).Split(Path.DirectorySeparatorChar).Contains("frames"))
            .Where(f => !string.Equals(Path.GetFileName(f), "status.json", StringComparison.Ordinal))
            .OrderBy(f => f, StringComparer.Ordinal);

    private static bool IsBuildOutput(string relative) =>
        relative.Split('/').Any(part => part is "bin" or "obj");

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static string List(IEnumerable<string> items) =>
        Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", items) + Environment.NewLine;

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Directory.Build.props")))
            dir = dir.Parent;
        dir.Should().NotBeNull("the pin reads docs/design, so it has to find the tree");
        return dir!.FullName;
    }
}
