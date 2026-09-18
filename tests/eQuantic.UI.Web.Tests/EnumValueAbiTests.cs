using System.Reflection;
using System.Runtime.CompilerServices;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A vocabulary enum's NUMBERS are an ABI, and this is the instrument that says so.
///
/// <para>
/// C# writes an enum constant into the CONSUMING assembly's IL, not a reference to it. So a member
/// inserted in the middle renumbers every member after it, and an app compiled against the previous
/// package goes on emitting the old number: the framework's own switches then read it as the member
/// that took its place. It is silent, it survives a clean build of the framework, and no test in
/// this repository could see it — <c>SemanticRole.ProgressIndicator</c> was added before
/// <c>GridCell</c>, moving GridCell from 9 to 10, and every suite stayed green.
/// </para>
///
/// <para>
/// THE RULE IS APPEND-ONLY, not "do not touch": a new member at the END takes the next free number
/// and breaks nobody, which is why this pin lets the baseline GROW and refuses only a value that
/// MOVED or a member that VANISHED. Explicit values are equally fine — the pin reads what the
/// compiler assigned either way.
/// </para>
///
/// <para>
/// <c>[Flags]</c> enums are included on purpose. Their members inline exactly the same way, and a
/// renumbered flag is worse: it silently changes what a COMBINATION means.
/// </para>
///
/// <para>Regenerate after an intentional append: <c>EQ_UPDATE_ENUM_VALUES=1 dotnet test
/// tests/eQuantic.UI.Web.Tests</c>. The baseline may grow; a line that changes is the bug.</para>
/// </summary>
public class EnumValueAbiTests
{
    private static string BaselinePath([CallerFilePath] string sourcePath = "") =>
        Path.Combine(Path.GetDirectoryName(sourcePath)!, "Coverage", "enum-values.baseline.txt");

    /// <summary>Every public enum of the vocabulary, flags included, as
    /// <c>Type.Member = value</c> lines ordered so the file is stable.</summary>
    private static string[] Current() =>
        typeof(VisualNode).Assembly.GetTypes()
            .Where(type => type.IsEnum && type.IsPublic)
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .SelectMany(type => Enum.GetNames(type)
                .Select(name => (Name: name,
                    Value: Convert.ToInt64(Enum.Parse(type, name), System.Globalization.CultureInfo.InvariantCulture)))
                .OrderBy(member => member.Value)
                .ThenBy(member => member.Name, StringComparer.Ordinal)
                .Select(member => $"{type.Name}.{member.Name} = {member.Value}"))
            .ToArray();

    [Fact]
    public void NoEnumMemberEverChangesItsNumber()
    {
        var current = Current();
        var path = BaselinePath();

        if (Environment.GetEnvironmentVariable("EQ_UPDATE_ENUM_VALUES") == "1")
        {
            File.WriteAllLines(path, current);
            return;
        }

        File.Exists(path).Should().BeTrue(
            $"the ABI baseline lives at {path} — run once with EQ_UPDATE_ENUM_VALUES=1");

        var live = current.ToHashSet(StringComparer.Ordinal);
        var broken = File.ReadAllLines(path)
            .Where(line => line.Length > 0 && !live.Contains(line))
            .ToArray();

        string.Join("\n", broken).Should().BeEmpty(
            "every one of these members had this number in a shipped package, and a consumer compiled "
            + "against it embedded the number rather than a reference. A member that MOVED renumbers "
            + "what a released app already emits; one that VANISHED leaves that app emitting a number "
            + "nothing answers. Append the new member at the END instead, then refresh with "
            + "EQ_UPDATE_ENUM_VALUES=1");
    }

    /// <summary>
    /// The pin is only worth its file if it FAILS, so this states what breaks it in terms of the
    /// actual defect: SemanticRole's roles keep the numbers they shipped with, and the newest one
    /// is last.
    /// </summary>
    [Fact]
    public void TheNewestRoleTookTheNextFreeNumberRatherThanSomeoneElses()
    {
        ((byte)SemanticRole.GridCell).Should().Be(9,
            "GridCell shipped as 9 — ProgressIndicator was inserted above it once and took it to 10");
        ((byte)SemanticRole.ProgressIndicator).Should().Be(10,
            "it took the next free number rather than someone else's — and this asserts the SHIPPED "
            + "value, not that it is still the highest: a role appended after it keeps 10 correct "
            + "while `Max()` would make this fail on the very append the contract above allows");
    }
}
