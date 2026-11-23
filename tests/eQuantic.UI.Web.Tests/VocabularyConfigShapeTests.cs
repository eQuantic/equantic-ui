using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The config each class of the hand-written mirror takes holds what its C# class lets an object initializer set,
/// and nothing C# does not have (#1823). A transpiled initializer is typed against the config the twin's constructor
/// takes, so a member the config lacks is a TS2353 the runtime's `tsc` raises only once some component first sets
/// it: `BoxStyleConfig` lacked `foreground`, carried by `BoxStyle` since #614, until a slice of #1606 set it and had
/// to stop and repair the mirror. A member the config declares and C# no longer has would never be met at all.
/// <para>
/// A config is a named interface (<c>BoxStyleConfig</c>), or a literal the constructor writes in place, alone or
/// intersected with interfaces (<c>VisualNodeConfig &amp; { … }</c>). <c>EqConfig</c>, a record of anything, takes
/// every member and so can lack none: a class that takes it is not read. Each config is read both ways: the C#
/// class's public settable members (set or init, inherited ones included) against the config's members, those it
/// extends or intersects included. A member the constructor also takes as a parameter is no exception: eqc writes
/// what an initializer sets into the config, wherever else the constructor could have taken it
/// (<c>new Image(src, 1, 2) { Label = "x" }</c> is <c>new Image(src, 1, 2, 'cover', '', { label: 'x' })</c>). A
/// member absent on purpose is named in <c>vocabulary-config.baseline.txt</c> with its reason, by its config or by
/// the C# type that declares it, and the list may only shrink: an entry that no longer differs fails until it is
/// removed.
/// </para>
/// </summary>
public class VocabularyConfigShapeTests
{
    private static string RepoRoot([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    private static string BaselinePath([CallerFilePath] string sourcePath = "") =>
        Path.Combine(Path.GetDirectoryName(sourcePath)!, "vocabulary-config.baseline.txt");

    /// <summary>A comment, which the reading skips: one naming <c>constructor(</c> or holding a brace would
    /// otherwise be read as code, and the class it sits in skipped or misread without a word. A <c>//</c> after a
    /// colon is an address's, not a comment's.</summary>
    private static readonly Regex Comment = new(@"/\*[\s\S]*?\*/|(?<!:)//[^\n]*", RegexOptions.Compiled);

    /// <summary>The hand-written mirrors, as <see cref="VocabularyMirrorShapeTests"/> reads them, without their comments.</summary>
    private static string MirrorText() => Comment.Replace(string.Join("\n",
        new[] { "vocabulary.ts", "value-types.ts", "primitive-values.ts", "route-values.ts" }
            .Select(name => File.ReadAllText(Path.Combine(RepoRoot(), "src", "eQuantic.UI.Runtime", "src", "shared", name)))), "");

    /// <summary>
    /// The text between the bracket just before <paramref name="open"/> and the one that closes it: a brace by
    /// default, or the parenthesis of a parameter list.
    /// </summary>
    private static string Body(string text, int open, char opening = '{', char closing = '}')
    {
        var depth = 1;
        var index = open;
        while (index < text.Length && depth > 0)
        {
            if (text[index] == opening) depth++;
            else if (text[index] == closing) depth--;
            index++;
        }
        return text[open..(index - 1)];
    }

    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    /// <summary>
    /// The members a type literal's body declares at its own level: whatever nests in braces, parentheses or
    /// brackets (a nested literal, a function type's parameters) is read as nothing.
    /// </summary>
    private static HashSet<string> TopLevelMembers(string body)
    {
        var top = new StringBuilder();
        var depth = 0;
        foreach (var character in body)
        {
            if (character is '{' or '(' or '[') depth++;
            else if (character is '}' or ')' or ']') depth--;
            else if (depth == 0) top.Append(character);
        }
        return Regex.Matches(top.ToString(), @"(?:^|[;,\n])\s*(?:readonly\s+)?(\w+)\??\s*:")
            .Select(member => member.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Each config interface's own members and the interfaces it extends.</summary>
    private static Dictionary<string, (HashSet<string> Members, string[] Extends)> Interfaces(string text) =>
        Regex.Matches(text, @"interface (\w+Config)(?:\s+extends\s+([\w,\s]+?))?\s*\{")
            .ToDictionary(
                match => match.Groups[1].Value,
                match => (
                    TopLevelMembers(Body(text, match.Index + match.Length)),
                    match.Groups[2].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));

    /// <summary>An interface's members with the ones it extends.</summary>
    private static HashSet<string> MembersOf(string name, Dictionary<string, (HashSet<string> Members, string[] Extends)> interfaces)
    {
        var (members, extends) = interfaces[name];
        var all = new HashSet<string>(members, StringComparer.Ordinal);
        foreach (var parent in extends.Where(interfaces.ContainsKey)) all.UnionWith(MembersOf(parent, interfaces));
        return all;
    }

    /// <summary>
    /// The config parameter of a class's constructor: its name, the interface itself when it is one alone and the class's
    /// own otherwise (<c>Image.config</c>), with its members, those of the interfaces it extends or intersects
    /// (<c>VisualNodeConfig &amp; { … }</c>) and those its literal writes in place.
    /// </summary>
    private static (string Name, HashSet<string> Members, string[] Named)? ConfigOf(
        string type, string body, Dictionary<string, (HashSet<string> Members, string[] Extends)> interfaces)
    {
        var constructor = body.IndexOf("constructor(", StringComparison.Ordinal);
        if (constructor < 0) return null;
        var parameters = Body(body, constructor + "constructor(".Length, '(', ')');
        var match = Regex.Match(parameters, @"\bconfig\??\s*:\s*((?:\w+\s*&\s*)*)(\{|\w+)");
        if (!match.Success) return null;
        var named = Regex.Matches(match.Groups[1].Value, @"\w+").Select(name => name.Value).ToList();
        var literal = match.Groups[2].Value == "{";
        if (!literal) named.Add(match.Groups[2].Value);
        var members = literal ? TopLevelMembers(Body(parameters, match.Index + match.Length)) : new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in named.Where(interfaces.ContainsKey)) members.UnionWith(MembersOf(name, interfaces));
        var label = !literal && named.Count == 1 ? named[0] : $"{type}.config";
        return (label, members, named.ToArray());
    }

    /// <summary>Each line of the baseline that names a member and the reason it is absent.</summary>
    private static Dictionary<string, string> Baseline() =>
        File.ReadAllLines(BaselinePath())
            .Where(line => line.Trim().Length > 0 && !line.TrimStart().StartsWith('#'))
            .Select(line => line.Split('|', 2, StringSplitOptions.TrimEntries))
            .ToDictionary(parts => parts[0], parts => parts.Length > 1 ? parts[1] : "", StringComparer.Ordinal);

    [Fact]
    public void EveryConfig_HoldsWhatItsClassLetsAnInitializerSet_AndNothingElse()
    {
        var text = MirrorText();
        var interfaces = Interfaces(text);
        interfaces.Should().HaveCountGreaterThan(10, "the mirror's config interfaces have to be read at all");
        var csharp = VocabularyTypesByTwinName.Get();
        var baseline = Baseline();
        baseline.Values.Should().NotContain(string.Empty, "every entry of the baseline says why the member is absent");
        var used = new HashSet<string>(StringComparer.Ordinal);
        var taken = new HashSet<string>(StringComparer.Ordinal);
        // A config typed as a record of anything (`EqConfig`) takes every member, so it can lack none.
        var open = Regex.Matches(text, @"export type (\w+) = Record<string, unknown>;")
            .Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        open.Should().NotBeEmpty("the mirror's open config has to be recognised, or each class taking it reads as drift");
        var offenders = new List<string>();
        var read = 0;

        foreach (Match declaration in Regex.Matches(text, @"export (?:abstract )?class (\w+)[^{]*\{"))
        {
            var taker = declaration.Groups[1].Value;
            if (ConfigOf(taker, Body(text, declaration.Index + declaration.Length), interfaces) is not var (name, members, named))
                continue;
            if (named.Any(open.Contains)) continue;
            read++;
            taken.UnionWith(named);
            foreach (var unknown in named.Where(type => !interfaces.ContainsKey(type)))
                offenders.Add($"{taker} takes its config as {unknown}, which is no config interface of the mirror");
            if (!csharp.TryGetValue(taker, out var type))
            {
                offenders.Add($"{taker} takes {name}, and mirrors no C# type");
                continue;
            }
            var settable = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.SetMethod is { IsPublic: true })
                .GroupBy(property => Camel(property.Name))
                .ToDictionary(group => group.Key, group => group.First().DeclaringType!.Name, StringComparer.Ordinal);

            foreach (var (member, declaredBy) in settable.Where(pair => !members.Contains(pair.Key)))
            {
                var byConfig = $"{name}.{member}";
                var byType = $"{declaredBy}.{member}";
                if (baseline.ContainsKey(byConfig)) used.Add(byConfig);
                else if (baseline.ContainsKey(byType)) used.Add(byType);
                else offenders.Add($"{byConfig}: {taker} lets an initializer set {declaredBy}.{member}, and the config lacks it");
            }
            foreach (var member in members.Where(member => !settable.ContainsKey(member)))
            {
                var byConfig = $"{name}.{member}";
                if (baseline.ContainsKey(byConfig)) used.Add(byConfig);
                else offenders.Add($"{byConfig}: the config declares it, and {taker} has no settable {member}");
            }
        }

        read.Should().BeGreaterThan(30, "the classes that take a config have to be read at all");
        offenders.AddRange(interfaces.Keys
            .Where(name => !taken.Contains(name) && !interfaces.Values.Any(other => other.Extends.Contains(name)))
            .Select(name => $"{name} is taken by no class of the mirror, so nothing pins it to C#"));
        offenders.AddRange(baseline.Keys.Where(key => !used.Contains(key))
            .Select(key => $"{key} is in vocabulary-config.baseline.txt and no longer differs: remove it, the list may only shrink"));
        string.Join("\n", offenders).Should().BeEmpty("a transpiled initializer is typed against the config its twin takes, "
            + "so a member it lacks fails the runtime's tsc only once some component first sets it");
    }
}
