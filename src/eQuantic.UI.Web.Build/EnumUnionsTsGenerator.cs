using System.Reflection;
using eQuantic.UI.Code;
using eQuantic.UI.Codegen;
using eQuantic.UI.Primitives;
using eQuantic.UI.Sheet;

namespace eQuantic.UI.Web.Build;

/// <summary>
/// Generates the TypeScript module that NAMES every vocabulary enum on the other side
/// (<c>src/shared/enums.generated.ts</c>): one string union per non-flags enum of
/// <c>eQuantic.UI.Primitives</c> and of the two engines beside it (<c>eQuantic.UI.Code</c> and
/// <c>eQuantic.UI.Sheet</c>), with the camelCase member strings the transpiler emits.
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
        var ts = new CodeWriter();
        ts.AppendLine("/**");
        ts.AppendLine(" * GENERATED — do not edit. One string union per non-flags enum of the C# vocabulary");
        ts.AppendLine(" * (eQuantic.UI.Primitives, eQuantic.UI.Code and eQuantic.UI.Sheet), spelled as the");
        ts.AppendLine(" * transpiler emits its members: camelCase strings.");
        ts.AppendLine(" * Regenerate: EQ_UPDATE_ENUMS_TS=1 dotnet test eQuantic.UI.Web.Tests");
        ts.AppendLine(" * (EnumUnionsTsGeneratorTests pins this file byte-for-byte against the generator).");
        ts.AppendLine(" *");
        ts.AppendLine(" * A [Flags] enum is absent by design: its members combine, so it crosses as a number.");
        ts.AppendLine(" *");
        ts.AppendLine(" * One table follows the unions: each FontWeight's number, which the runtime asks for by name.");
        ts.AppendLine(" */");

        foreach (var type in Unions())
        {
            // Distinct: an alias (two names, one value) is one member of the union, named twice.
            var members = Enum.GetNames(type)
                .Select(name => char.ToLowerInvariant(name[0]) + name[1..])
                .Distinct(StringComparer.Ordinal);

            ts.AppendLine();
            TsUnion.Write(ts, $"{type.Name}Value", members);
        }

        // A weight is the one vocabulary enum whose VALUE is its meaning, the weight class CSS, CoreText,
        // DirectWrite and Android are all asked for, while the transpiler emits a member as its name. So
        // the runtime reads the number here, written from the enum: the switch it read it from was written
        // by hand, and the four weights appended to the enum fell to its default, 400 (#1082).
        ts.AppendLine();
        ts.AppendLine("/** Each weight's number, its OpenType weight class, keyed by the name the transpiler emits. */");
        using (ts.BeginBlock("export const FontWeightNumbers: Readonly<Record<FontWeightValue, number>> = {", "};"))
            foreach (var weight in Enum.GetValues<FontWeight>().OrderBy(weight => (int)weight))
                ts.AppendLine($"{char.ToLowerInvariant(weight.ToString()[0])}{weight.ToString()[1..]}: {(int)weight},");

        return ts.ToString();
    }

    /// <summary>
    /// Every public non-flags enum of the vocabulary assemblies, in name order so the file is stable
    /// whatever order reflection hands them back. The same three the compiler routes as vocabulary
    /// (<c>RuntimeProvidedTypeScanner.VocabularyNamespaces</c>): an enum it annotates with a union
    /// this file does not declare is a transpiled twin that does not compile.
    /// </summary>
    public static IEnumerable<Type> Unions() =>
        new[] { typeof(VisualNode).Assembly, typeof(CodeEditorController).Assembly, typeof(SheetController).Assembly }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsEnum && type.IsPublic
                && type.GetCustomAttribute<FlagsAttribute>() is null)
            .OrderBy(type => type.Name, StringComparer.Ordinal);
}
