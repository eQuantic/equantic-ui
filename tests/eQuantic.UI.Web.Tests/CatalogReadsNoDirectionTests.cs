using System.Reflection;
using System.Reflection.Emit;
using eQuantic.UI.Charts;
using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Spreadsheets;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// NO COMPONENT OF THE CATALOG PICKS A GLYPH, A SIGN OR A KEY FROM THE DIRECTION (#1569). A component
/// speaks logically (start and end, previous and next, "toward the end", a glyph that matches the
/// direction) and the realizer binds that to what is physical where its element sits. A component that
/// asked <see cref="ComponentContext.TextDirection"/> and chose for itself would flip on Photon too, where
/// <c>TextDirection</c> answers the culture's and the layout still runs left to right: a back chevron
/// pointing right in a layout whose start is on the left.
/// <para>
/// Writing a <see cref="Directionality"/> stays allowed (a code view fences itself left to right with
/// one), because that SAYS a direction and reads none.
/// </para>
/// <para>
/// BOUND BY SYMBOL: the guard reads the compiled bodies of every method of the catalog's assemblies,
/// closures and state machines included, and asks whether an instruction calls the metadata member
/// <c>ComponentContext.TextDirection</c>, never whether a name is spelled in the source. A rename of the
/// property, a <c>var</c>, an alias, a lambda that captures the context: none of them hides a read.
/// </para>
/// </summary>
public class CatalogReadsNoDirectionTests
{
    private static readonly MethodInfo TextDirectionGetter =
        typeof(ComponentContext).GetProperty(nameof(ComponentContext.TextDirection))!.GetMethod!;

    /// <summary>The assemblies of the catalog, each named by a type it holds.</summary>
    private static readonly Assembly[] Catalog =
    [
        typeof(Button).Assembly,
        typeof(BarChart).Assembly,
        typeof(Spreadsheet).Assembly,
    ];

    /// <summary>The OpCodes by their encoding, to walk a method body instruction by instruction.</summary>
    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(code => code.Value);

    private const BindingFlags EveryMember = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
        | BindingFlags.Static | BindingFlags.DeclaredOnly;

    [Fact]
    public void NoComponentOfTheCatalog_ReadsTheDirectionItsContextHolds()
    {
        var readers = Catalog.SelectMany(assembly => ReadersIn(assembly.GetTypes())).ToList();

        readers.Should().BeEmpty(
            "the realizer binds what a component says logically to physical input and output by the direction "
            + "where the element sits; a component that reads ComponentContext.TextDirection flips on Photon too, "
            + "which does not mirror (a Directionality may be WRITTEN, and reads nothing)");
    }

    [Fact]
    public void TheGuard_SeesAComponentThatPicksAGlyphFromTheDirection()
    {
        // Exercised in the condition it exists for: a probe that does exactly what the wiki used to teach,
        // choosing a chevron from context.TextDirection, in a lambda as well as in Build.
        var readers = ReadersIn([typeof(BackChevron), .. typeof(BackChevron).GetNestedTypes(EveryMember)]).ToList();

        readers.Should().Contain(reader => reader.EndsWith(".Build", StringComparison.Ordinal),
            "the guard fails on a component that reads the direction");
        readers.Should().Contain(reader => reader.Contains("<Build>b__", StringComparison.Ordinal),
            "a read inside a lambda is a read too");
    }

    [Fact]
    public void TheGuard_PassesAComponentThatOnlyWritesADirectionality()
    {
        ReadersIn([typeof(FencedChevron)]).Should().BeEmpty("writing a Directionality reads no direction");
    }

    /// <summary>What the guard must catch: it picks the glyph itself, as the wiki's Localization page taught.</summary>
    private sealed class BackChevron : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            var back = context.TextDirection == TextDirection.RightToLeft ? Icons.ChevronRight : Icons.ChevronLeft;
            Func<Icons> again = () => context.TextDirection == TextDirection.RightToLeft ? Icons.ChevronRight : Icons.ChevronLeft;
            return new Row { new Icon(back), new Icon(again()) };
        }
    }

    /// <summary>What it must let through: a fence says a direction and reads none.</summary>
    private sealed class FencedChevron : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) =>
            new Directionality(TextDirection.LeftToRight, new Icon(Icons.ChevronRight));
    }

    /// <summary>Every method of <paramref name="types"/> whose body calls the getter of
    /// <see cref="ComponentContext.TextDirection"/>, as <c>Type.Method</c>.</summary>
    private static IEnumerable<string> ReadersIn(IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            foreach (var method in type.GetMethods(EveryMember).Cast<MethodBase>().Concat(type.GetConstructors(EveryMember)))
            {
                if (method.GetMethodBody()?.GetILAsByteArray() is not { } il) continue;
                if (CallsTheGetter(method, il)) yield return $"{type.FullName}.{method.Name}";
            }
        }
    }

    private static bool CallsTheGetter(MethodBase method, byte[] il)
    {
        var typeArguments = method.DeclaringType!.IsGenericType ? method.DeclaringType.GetGenericArguments() : null;
        var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
        foreach (var token in MethodTokens(il))
        {
            MethodBase? callee;
            try
            {
                callee = method.Module.ResolveMethod(token, typeArguments, methodArguments);
            }
            catch (ArgumentException)
            {
                continue; // a token that names no method of this module in this generic context
            }
            if (callee is not null && callee.MetadataToken == TextDirectionGetter.MetadataToken
                && callee.Module == TextDirectionGetter.Module) return true;
        }
        return false;
    }

    /// <summary>The method tokens an IL body names: the operand of each call, callvirt, newobj, ldftn.</summary>
    private static IEnumerable<int> MethodTokens(byte[] il)
    {
        var at = 0;
        while (at < il.Length)
        {
            var value = il[at] == 0xFE ? unchecked((short)(0xFE00 | il[at + 1])) : il[at];
            var code = OpCodesByValue[value];
            at += code.Size;
            switch (code.OperandType)
            {
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar:
                    at += 1;
                    break;
                case OperandType.InlineVar:
                    at += 2;
                    break;
                case OperandType.InlineI8 or OperandType.InlineR:
                    at += 8;
                    break;
                case OperandType.InlineSwitch:
                    at += 4 + 4 * BitConverter.ToInt32(il, at);
                    break;
                case OperandType.InlineMethod:
                    yield return BitConverter.ToInt32(il, at);
                    at += 4;
                    break;
                default: // the other four-byte operands: a field, a type, a string, a signature, a branch
                    at += 4;
                    break;
            }
        }
    }
}
