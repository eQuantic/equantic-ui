using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The hand-written runtime mirror has to agree with the C# it mirrors about the SHAPE of every
/// static member, not only its name.
///
/// <para>
/// `VectorPaint.None` is a static FIELD in C#, so eqc emits a field access — `VectorPaint.none`.
/// The mirror declared it as a static METHOD, so the browser handed the component the function
/// itself: the realizer read `kind` off it, found undefined, and drew nothing. Across one site that
/// was 79 references, and every one of them looked right in C#, compiled, type-checked, and
/// rendered perfectly from the server — where the fields are real. Only the hydrated page was
/// wrong, which is the hardest place to be wrong in.
/// </para>
/// <para>
/// Nothing else could have caught it: the emitted TypeScript is bundled by bun, which strips types
/// without checking them, so no compiler on either side ever compares the two declarations. This
/// test is that comparison.
/// </para>
/// </summary>
public class VocabularyMirrorShapeTests
{
    private static string RepoRoot([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    /// <summary>
    /// Every HAND-WRITTEN mirror. The transpiled modules are not here: they are emitted from the
    /// same C# they mirror and cannot disagree with it. These four are typed by a person, which is
    /// the only way the two declarations ever drift apart.
    /// </summary>
    private static string[] MirrorPaths() =>
        new[] { "vocabulary.ts", "value-types.ts", "primitive-values.ts", "route-values.ts" }
            .Select(name => Path.Combine(RepoRoot(),
                "src", "eQuantic.UI.Runtime", "src", "shared", name))
            .ToArray();

    /// <summary>Each `export class X { … }` in the mirror, as its own text. Brace-counted rather
    /// than regex-matched: a class body is full of braces, and a lazy match stopped at the first
    /// method's closing one.</summary>
    private static Dictionary<string, string> MirrorClasses()
    {
        var classes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in MirrorPaths())
        {
        var text = File.ReadAllText(path);

        foreach (Match match in Regex.Matches(text, @"export class (\w+)[^{]*\{"))
        {
            var start = match.Index + match.Length;
            var depth = 1;
            var index = start;
            while (index < text.Length && depth > 0)
            {
                if (text[index] == '{') depth++;
                else if (text[index] == '}') depth--;
                index++;
            }
            classes[match.Groups[1].Value] = text[start..(index - 1)];
        }
        }

        return classes;
    }

    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    [Fact]
    public void EveryMirroredStaticMember_KeepsTheShapeItHasInCSharp()
    {
        var mirror = MirrorClasses();
        mirror.Should().HaveCountGreaterThan(10, "the mirror has to be parsed at all");

        var primitives = typeof(Primitives.VisualNode).Assembly.GetTypes()
            .Where(type => type.IsPublic)
            .GroupBy(type => type.Name)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var offenders = new List<string>();
        foreach (var (name, body) in mirror)
        {
            if (!primitives.TryGetValue(name, out var csharp)) continue;

            // A FIELD is read without parentheses on both sides, so a method in the mirror hands
            // the caller a function where a value was meant.
            foreach (var field in csharp.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var member = Camel(field.Name);
                if (Regex.IsMatch(body, $@"static\s+{Regex.Escape(member)}\s*\("))
                    offenders.Add($"{name}.{member} is a field in C# and a method in the mirror");
            }

            // And the same drift the other way: a C# method mirrored as a value is a call on
            // something that is not a function, which is at least loud — but still only in a browser.
            foreach (var method in csharp.GetMethods(BindingFlags.Public | BindingFlags.Static
                         | BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName))
            {
                var member = Camel(method.Name);
                if (Regex.IsMatch(body, $@"static\s+(readonly\s+)?{Regex.Escape(member)}\s*[:=]"))
                    offenders.Add($"{name}.{member} is a method in C# and a value in the mirror");
            }
        }

        offenders.Should().BeEmpty("eqc emits the access the C# DECLARATION implies, and the "
            + "browser gets whatever the mirror declared — a disagreement is a component that "
            + "renders from the server and breaks on hydration");
    }
}
