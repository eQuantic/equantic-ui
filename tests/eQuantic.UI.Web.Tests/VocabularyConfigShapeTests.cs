using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Each config interface of the hand-written mirror holds what its C# class lets an object initializer set, and
/// nothing C# does not have (#1823). A transpiled initializer is typed against the interface the twin's constructor
/// takes, so a member the interface lacks is a TS2353 the runtime's `tsc` raises only once some component first
/// sets it: `BoxStyleConfig` lacked `foreground`, carried by `BoxStyle` since #614, until a slice of #1606 set it and
/// had to stop and repair the mirror. A member the interface declares and C# no longer has would never be met at all.
/// <para>
/// Read both ways: the C# class's public settable members (set or init, inherited ones included), less the ones its
/// constructor takes by position, against the interface's members, the ones it extends included. A member absent
/// on purpose is named in <c>vocabulary-config.baseline.txt</c> with its reason, by its config or by the C# type that
/// declares it, and the list may only shrink: an entry that no longer differs fails until it is removed.
/// </para>
/// </summary>
public class VocabularyConfigShapeTests
{
    private static string RepoRoot([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    private static string BaselinePath([CallerFilePath] string sourcePath = "") =>
        Path.Combine(Path.GetDirectoryName(sourcePath)!, "vocabulary-config.baseline.txt");

    /// <summary>The hand-written mirrors, as <see cref="VocabularyMirrorShapeTests"/> reads them.</summary>
    private static string MirrorText() => string.Join("\n",
        new[] { "vocabulary.ts", "value-types.ts", "primitive-values.ts", "route-values.ts" }
            .Select(name => File.ReadAllText(Path.Combine(RepoRoot(), "src", "eQuantic.UI.Runtime", "src", "shared", name))));

    /// <summary>The text between the brace that opens at <paramref name="open"/> and the one that closes it.</summary>
    private static string Body(string text, int open)
    {
        var depth = 1;
        var index = open;
        while (index < text.Length && depth > 0)
        {
            if (text[index] == '{') depth++;
            else if (text[index] == '}') depth--;
            index++;
        }
        return text[open..(index - 1)];
    }

    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    /// <summary>Each config interface's own members and the interfaces it extends.</summary>
    private static Dictionary<string, (HashSet<string> Members, string[] Extends)> Interfaces(string text) =>
        Regex.Matches(text, @"interface (\w+Config)(?:\s+extends\s+([\w,\s]+?))?\s*\{")
            .ToDictionary(
                match => match.Groups[1].Value,
                match => (
                    Regex.Matches(Body(text, match.Index + match.Length), @"^\s{2}(\w+)\??\s*:", RegexOptions.Multiline)
                        .Select(member => member.Groups[1].Value).ToHashSet(StringComparer.Ordinal),
                    match.Groups[2].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));

    /// <summary>An interface's members with the ones it extends.</summary>
    private static HashSet<string> MembersOf(string name, Dictionary<string, (HashSet<string> Members, string[] Extends)> interfaces)
    {
        var (members, extends) = interfaces[name];
        var all = new HashSet<string>(members, StringComparer.Ordinal);
        foreach (var parent in extends.Where(interfaces.ContainsKey)) all.UnionWith(MembersOf(parent, interfaces));
        return all;
    }

    /// <summary>Each line of the baseline that names a member and the reason it is absent.</summary>
    private static Dictionary<string, string> Baseline() =>
        File.ReadAllLines(BaselinePath())
            .Where(line => line.Trim().Length > 0 && !line.TrimStart().StartsWith('#'))
            .Select(line => line.Split('|', 2, StringSplitOptions.TrimEntries))
            .ToDictionary(parts => parts[0], parts => parts.Length > 1 ? parts[1] : "", StringComparer.Ordinal);

    [Fact]
    public void EveryConfigInterface_HoldsWhatItsClassLetsAnInitializerSet_AndNothingElse()
    {
        var text = MirrorText();
        var interfaces = Interfaces(text);
        interfaces.Should().HaveCountGreaterThan(10, "the mirror's config interfaces have to be read at all");
        var classes = Regex.Matches(text, @"export (?:abstract )?class (\w+)[^{]*\{")
            .Select(match => (Name: match.Groups[1].Value, Body: Body(text, match.Index + match.Length)))
            .ToList();
        var csharp = VocabularyTypesByTwinName.Get();
        var baseline = Baseline();
        baseline.Values.Should().NotContain(string.Empty, "every entry of the baseline says why the member is absent");
        var used = new HashSet<string>(StringComparer.Ordinal);
        var offenders = new List<string>();

        foreach (var name in interfaces.Keys.Where(name => !interfaces.Values.Any(other => other.Extends.Contains(name))))
        {
            var takers = classes.Where(type => Regex.IsMatch(type.Body, $@"config\??\s*:\s*{name}\b")).Select(type => type.Name).ToList();
            if (takers.Count == 0) offenders.Add($"{name} is taken by no class of the mirror, so nothing pins it to C#");
            var members = MembersOf(name, interfaces);

            foreach (var taker in takers)
            {
                if (!csharp.TryGetValue(taker, out var type))
                {
                    offenders.Add($"{name} is taken by {taker}, which mirrors no C# type");
                    continue;
                }
                var settable = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(property => property.SetMethod is { IsPublic: true })
                    .GroupBy(property => Camel(property.Name))
                    .ToDictionary(group => group.Key, group => group.First().DeclaringType!.Name, StringComparer.Ordinal);
                var positional = type.GetConstructors()
                    .SelectMany(constructor => constructor.GetParameters())
                    .Select(parameter => Camel(parameter.Name!))
                    .ToHashSet(StringComparer.Ordinal);

                foreach (var (member, declaredBy) in settable.Where(pair => !members.Contains(pair.Key) && !positional.Contains(pair.Key)))
                {
                    var byConfig = $"{name}.{member}";
                    var byType = $"{declaredBy}.{member}";
                    if (baseline.ContainsKey(byConfig)) used.Add(byConfig);
                    else if (baseline.ContainsKey(byType)) used.Add(byType);
                    else offenders.Add($"{byConfig}: {taker} lets an initializer set {declaredBy}.{member}, and the config lacks it");
                }
                foreach (var member in members.Where(member => !settable.ContainsKey(member) && !positional.Contains(member)))
                {
                    var byConfig = $"{name}.{member}";
                    if (baseline.ContainsKey(byConfig)) used.Add(byConfig);
                    else offenders.Add($"{byConfig}: the config declares it, and {taker} has no settable {member} nor a parameter of that name");
                }
            }
        }

        offenders.AddRange(baseline.Keys.Where(key => !used.Contains(key))
            .Select(key => $"{key} is in vocabulary-config.baseline.txt and no longer differs: remove it, the list may only shrink"));
        string.Join("\n", offenders).Should().BeEmpty("a transpiled initializer is typed against the config its twin takes, "
            + "so a member it lacks fails the runtime's tsc only once some component first sets it");
    }
}
