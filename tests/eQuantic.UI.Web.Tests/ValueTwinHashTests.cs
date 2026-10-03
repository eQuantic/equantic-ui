using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A vocabulary value type's HAND-WRITTEN twin hashes by its members, as <c>$eq.equals</c> compares
/// it. <c>Point</c>, <c>Size</c>, <c>Rect</c> and <c>TypeStyle</c> are record structs in C#, so two
/// <c>new Point(1, 2)</c> are equal and hash equal there, and their twins are runtime classes, which
/// the runtime's hash takes by identity unless the class is registered with <c>hashesByValue</c>
/// (utils/hash.ts). Unregistered, equal points hashed apart, and so did every record holding one
/// (Copilot on #550).
/// <para>
/// The scan is derived, never listed: every <c>export class</c> in the runtime's top-level shared
/// files, paired by name with the vocabulary's public types. A class that twins a struct or a record
/// must be registered in its own file, and a class registered must twin one, so the list cannot rot
/// in either direction. The transpiled modules (<c>shared/components</c>) carry the
/// <c>getHashCode</c> eqc writes and are not scanned.
/// </para>
/// </summary>
public class ValueTwinHashTests
{
    private static string RepoRoot([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    [Fact]
    public void EveryHandWrittenTwinOfAValueType_HashesByValue_AndNoOtherClassDoes()
    {
        var shared = Path.Combine(RepoRoot(), "src", "eQuantic.UI.Runtime", "src", "shared");
        var vocabulary = typeof(Primitives.VisualNode).Assembly.GetTypes()
            .Where(type => type.IsPublic)
            .GroupBy(type => type.Name)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var offenders = new List<string>();
        var twins = 0;
        foreach (var path in Directory.GetFiles(shared, "*.ts").Where(path => !path.EndsWith(".spec.ts", StringComparison.Ordinal)))
        {
            var text = File.ReadAllText(path);
            var file = Path.GetFileName(path);
            var classes = Regex.Matches(text, @"^export class (\w+)", RegexOptions.Multiline)
                .Select(match => match.Groups[1].Value)
                .ToHashSet(StringComparer.Ordinal);
            var registered = Regex.Matches(text, @"hashesByValue\(([^)]*)\)")
                .SelectMany(match => match.Groups[1].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                .ToHashSet(StringComparer.Ordinal);

            foreach (var name in classes)
            {
                var byValue = vocabulary.TryGetValue(name, out var type) && HasValueEquality(type);
                if (byValue) twins++;
                if (byValue && !registered.Contains(name))
                    offenders.Add($"{file}: {name} twins a value type and does not hash by value");
                if (!byValue && registered.Contains(name))
                    offenders.Add($"{file}: {name} hashes by value and twins no value type of the vocabulary");
            }
            foreach (var name in registered.Where(name => !classes.Contains(name)))
                offenders.Add($"{file}: registers {name}, which the file does not declare");
        }

        twins.Should().BeGreaterThan(10, "the scan has to find the hand-written twins at all");
        string.Join(Environment.NewLine, offenders).Should().BeEmpty(
            "a value type's twin hashes as `$eq.equals` compares it, member by member; register it with "
            + "`hashesByValue` in its own file (utils/hash.ts)");
    }

    /// <summary>A struct, or a class record, whose equality is its members'.</summary>
    private static bool HasValueEquality(Type type) =>
        (type.IsValueType && !type.IsEnum) || type.GetMethod("<Clone>$") is not null;
}
