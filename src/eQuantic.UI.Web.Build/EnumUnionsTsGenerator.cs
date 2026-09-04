using System.Reflection;
using System.Text;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Build;

/// <summary>
/// Generates the TypeScript module that NAMES every vocabulary enum on the other side
/// (<c>src/shared/enums.generated.ts</c>): one string union per non-flags enum of
/// <c>eQuantic.UI.Primitives</c>, with the camelCase member strings the transpiler emits.
/// <para>
/// The unions exist so a transpiled component can FORWARD its own enum property into a vocabulary
/// slot. A property declared <c>string</c> is wider than a slot declared <c>MainAlignValue</c>, so
/// the twin stopped compiling the first time a component passed one on; comparing against enum
/// members never needed the narrower type, which is why it took a rail's alignment knob to surface.
/// </para>
/// <para>
/// GENERATED rather than hand-mirrored for the reason the design system is: a union written by hand
/// silently drifts the day someone adds an enum member, and a drifted union is a type that LIES —
/// worse than the bare string it replaced. A [Flags] enum has no union here at all: its members
/// combine, so its runtime value is a number.
/// </para>
/// </summary>
public static class EnumUnionsTsGenerator
{
    public static string Generate()
    {
        var ts = new StringBuilder();
        ts.AppendLine("/**");
        ts.AppendLine(" * GENERATED — do not edit. One string union per non-flags enum of the C# vocabulary");
        ts.AppendLine(" * (eQuantic.UI.Primitives), spelled as the transpiler emits its members: camelCase strings.");
        ts.AppendLine(" * Regenerate: EQ_UPDATE_ENUMS_TS=1 dotnet test eQuantic.UI.Web.Tests");
        ts.AppendLine(" * (EnumUnionsTsGeneratorTests pins this file byte-for-byte against the generator).");
        ts.AppendLine(" *");
        ts.AppendLine(" * A [Flags] enum is absent by design: its members combine, so it crosses as a number.");
        ts.AppendLine(" */");

        foreach (var type in Unions())
        {
            // Distinct: an alias (two names, one value) is one member of the union, named twice.
            var members = Enum.GetNames(type)
                .Select(name => $"'{char.ToLowerInvariant(name[0]) + name[1..]}'")
                .Distinct(StringComparer.Ordinal)
                .ToList();

            ts.AppendLine();
            // Long unions wrap: Icons alone is a curated set of dozens, and a single line of it is
            // unreadable in a diff.
            var oneLine = $"export type {type.Name}Value = {string.Join(" | ", members)};";
            if (oneLine.Length <= 100)
            {
                ts.AppendLine(oneLine);
                continue;
            }

            ts.AppendLine($"export type {type.Name}Value =");
            var line = new StringBuilder("  ");
            foreach (var member in members)
            {
                var piece = line.Length == 2 ? member : $" | {member}";
                if (line.Length + piece.Length > 100)
                {
                    ts.AppendLine(line.ToString());
                    line = new StringBuilder("  | " + member);
                    continue;
                }
                line.Append(piece);
            }
            ts.AppendLine(line.Append(';').ToString());
        }

        return ts.ToString();
    }

    /// <summary>Every public non-flags enum of the vocabulary, in name order so the file is stable
    /// whatever order reflection hands them back.</summary>
    public static IEnumerable<Type> Unions() =>
        typeof(VisualNode).Assembly.GetTypes()
            .Where(type => type.IsEnum && type.IsPublic
                && type.GetCustomAttribute<FlagsAttribute>() is null)
            .OrderBy(type => type.Name, StringComparer.Ordinal);
}
