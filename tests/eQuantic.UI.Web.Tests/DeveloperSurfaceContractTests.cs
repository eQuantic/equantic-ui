using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The developer's surface the API analyzer cannot see (#322). <c>PublicApiAnalyzers</c> holds
/// every C# signature; the product principle puts the rest of what a developer writes in three
/// other places — the SDKs' MSBuild properties in the csproj, the configuration sections in
/// appsettings, and the <c>dotnet new</c> template parameters — and renaming any of them is a
/// break a consumer meets as a setting silently ignored, never as a compiler error.
/// <para>
/// So this reads the three from the SOURCE — every <c>EQuantic…</c>/<c>Eqc…</c> property the two
/// SDKs' <c>Sdk.props</c>/<c>Sdk.targets</c> define or read, every configuration section name the
/// source binds, every template parameter and choice — and compares them with a committed
/// baseline. Regenerate with <c>EQ_UPDATE_DEVELOPER_SURFACE=1</c> and read the diff: in preview a
/// line may change with the diff read, and from 1.0 a line may only be added.
/// </para>
/// </summary>
public class DeveloperSurfaceContractTests
{
    private static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    private static string BaselinePath => Path.Combine(RepoRoot(), "tests", "eQuantic.UI.Web.Tests", "developer-surface.baseline.txt");

    /// <summary>A developer-facing property: the SDK's own prefix, never an underscore-prefixed
    /// private one. Read where it is set (an element) and where it is read (a reference).</summary>
    private static readonly Regex PropertyReference = new(@"\$\(((?:EQuantic|Eqc)[A-Za-z0-9]*)\)", RegexOptions.Compiled);
    private static readonly Regex PropertyElement = new(@"<((?:EQuantic|Eqc)[A-Za-z0-9]*)[\s/>]", RegexOptions.Compiled);

    /// <summary>A configuration section the source binds: a literal given to GetSection or
    /// BindConfiguration, or a SectionName constant.</summary>
    private static readonly Regex SectionBinding = new(
        @"(?:GetSection|BindConfiguration)\(\s*""([^""]+)""|SectionName\s*=\s*""([^""]+)""", RegexOptions.Compiled);

    private static IEnumerable<string> Surface()
    {
        var root = RepoRoot();
        foreach (var sdk in new[] { "eQuantic.UI.Sdk", "eQuantic.UI.Sdk.Native" })
        {
            var directory = Path.Combine(root, "src", sdk, "Sdk");
            var names = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var file in new[] { "Sdk.props", "Sdk.targets" }.Select(f => Path.Combine(directory, f)).Where(File.Exists))
            {
                var text = File.ReadAllText(file);
                foreach (Match match in PropertyReference.Matches(text)) names.Add(match.Groups[1].Value);
                foreach (Match match in PropertyElement.Matches(text)) names.Add(match.Groups[1].Value);
            }
            foreach (var name in names) yield return $"msbuild {sdk} {name}";
        }

        var sections = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
                     .Where(IsSource))
        {
            foreach (Match match in SectionBinding.Matches(File.ReadAllText(file)))
                sections.Add(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
        }
        foreach (var section in sections) yield return $"config {section}";

        var templates = Path.Combine(root, "src", "eQuantic.UI.Templates", "templates");
        foreach (var manifest in Directory.EnumerateFiles(templates, "template.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            using var json = JsonDocument.Parse(File.ReadAllText(manifest));
            var shortName = json.RootElement.GetProperty("shortName").GetString();
            if (!json.RootElement.TryGetProperty("symbols", out var symbols)) continue;
            foreach (var symbol in symbols.EnumerateObject().OrderBy(s => s.Name, StringComparer.Ordinal))
            {
                if (!symbol.Value.TryGetProperty("type", out var type) || type.GetString() != "parameter") continue;
                yield return $"template {shortName} --{symbol.Name}";
                if (!symbol.Value.TryGetProperty("choices", out var choices)) continue;
                foreach (var choice in choices.EnumerateArray().Select(c => c.GetProperty("choice").GetString()).Order(StringComparer.Ordinal))
                    yield return $"template {shortName} --{symbol.Name}={choice}";
            }
        }
    }

    private static bool IsSource(string path)
    {
        var separator = Path.DirectorySeparatorChar;
        return !path.Contains($"{separator}obj{separator}") && !path.Contains($"{separator}bin{separator}");
    }

    [Fact]
    public void TheSurfaceAnAppWritesOutsideCSharp_IsTheCommittedOne()
    {
        var current = Surface().ToList();
        current.Should().Contain(line => line.StartsWith("msbuild eQuantic.UI.Sdk ", StringComparison.Ordinal),
            "the scan must reach the SDK at all, or the comparison below holds on nothing");

        if (Environment.GetEnvironmentVariable("EQ_UPDATE_DEVELOPER_SURFACE") == "1")
        {
            File.WriteAllText(BaselinePath, string.Join("\n", current) + "\n");
            return;
        }

        File.Exists(BaselinePath).Should().BeTrue(
            "no committed baseline — run once with EQ_UPDATE_DEVELOPER_SURFACE=1 and commit the file");
        var committed = File.ReadAllText(BaselinePath).Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();

        var gone = committed.Except(current, StringComparer.Ordinal).ToList();
        var added = current.Except(committed, StringComparer.Ordinal).ToList();
        gone.Should().BeEmpty(
            "each of these is something an app's csproj, appsettings or `dotnet new` line may name, and "
            + "it no longer means anything — a setting that is now silently ignored. If the rename is "
            + "intended, regenerate with EQ_UPDATE_DEVELOPER_SURFACE=1 and put the line in the migration notes");
        added.Should().BeEmpty(
            "the developer's surface grew — regenerate with EQ_UPDATE_DEVELOPER_SURFACE=1 so the new "
            + "setting is on record");
    }
}
