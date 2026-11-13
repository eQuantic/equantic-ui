using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// An explicit cast. The bound tree names the conversion the cast performs, and the ONE conversion
/// table (<see cref="ValueFlow.Apply"/>) applies it — the same table that settles implicit flows
/// and <c>foreach</c> elements, so <c>(int)aLong</c> slices the BigInt's low 32 bits instead of
/// putting a BigInt into Math.trunc, and <c>checked((byte)n)</c> throws where C# throws. What
/// stays HERE is what only a cast does: a constant written as its target holds it, the unboxing of
/// an object into an enum, the checked context an enum's conversion reads off the syntax, and the
/// spelled-type fallback for the worlds without a semantic model.
/// </summary>
public class CastExpressionStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is CastExpressionSyntax;
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var cast = (CastExpressionSyntax)node;

        var targetType = context.SemanticHelper.GetType(cast);
        var operandType = UnwrapNullable(context.SemanticHelper.GetType(cast.Expression));

        // A cast from a nullable value into a type that is not nullable UNWRAPS it (ValueFlow.Unwraps): an
        // empty one throws before an enum's tables read it, as before any other conversion (#967). The one
        // table applies it to every other cast.
        var unwraps = context.SemanticHelper.GetOperation(cast) is IConversionOperation bound
            && ValueFlow.Unwraps(bound.GetConversion(), bound.Operand.Type, bound.Type);
        JsExpr Operand()
        {
            var converted = context.Converter.ConvertIr(cast.Expression);
            return unwraps ? ValueFlow.Unwrapped(converted, context) : converted;
        }

        // An ENUMERATION conversion, out of an enum into a number or into an enum from a number or another
        // enum (Types.EnumConversion): the value the enum holds, converted as its underlying type
        // converts. A 64-bit enum's value is a BigInt (#551): `(int)aWideFlag` takes its low 32 bits and
        // `(long)aWideFlag` is the BigInt itself, where a number past 2^53 had lost its low digits. The
        // bound tree reports an enumeration conversion unchecked inside `checked(…)` too, so the syntax
        // says where .NET throws.
        var target = UnwrapNullable(targetType);
        var enumSource = operandType is INamedTypeSymbol { TypeKind: TypeKind.Enum };
        var enumTarget = target as INamedTypeSymbol is { TypeKind: TypeKind.Enum } named ? named : null;
        if (enumSource && target is not null && IsNumeric(target) || enumTarget is not null)
        {
            // The CAST's own constant, the value already converted (`unchecked((Tiny)257)` is 1 with a byte
            // under it, `(long)Wide.Top` a long), written as the target holds it.
            if (context.SemanticHelper.TryGetConstantValue(cast, out var constant) && constant is not null)
            {
                if (enumTarget is not null)
                    return JsExpr.Literal(Types.EnumShape.HeldLiteral(enumTarget,
                        System.Convert.ToDecimal(constant, CultureInfo.InvariantCulture)));
                if (IntegerWidth.Of(target) is { } width)
                    return JsExpr.Literal(System.Convert.ToDecimal(constant, CultureInfo.InvariantCulture)
                        .ToString(CultureInfo.InvariantCulture) + (width.Bits == 64 ? "n" : ""));
            }

            var operandIr = Operand();
            // An object holding the enum (`(Status)Enum.Parse(typeof(Status), text)`) is unboxed, as the
            // runtime unboxes it: the member it holds, a boxed number's member, and a refusal for anything
            // else, where the value→key table answered undefined and a boxed string passed through.
            if (enumTarget is not null && operandType is not null && !IsNumeric(operandType) && !enumSource)
            {
                context.UsedHelpers.Add(Eq.Import);
                return JsExpr.Call(JsExpr.Identifier(Eq.EnumUnbox), operandIr,
                    JsExpr.Literal(Types.EnumShape.Of(enumTarget)), JsExpr.Literal(JsStringLiteral.Quote(Boxing.NameOf(enumTarget))));
            }
            // A value that is not null converts on its own; a nullable one kept nullable lets null pass.
            var from = unwraps ? operandType : context.SemanticHelper.GetType(cast.Expression);
            return Types.EnumConversion.Convert(from, targetType, null, operandIr,
                ArithmeticContext.ChecksOverflow(cast, context), context) ?? operandIr;
        }

        // The bound tree names the conversion — user-defined operator, numeric with its widths and
        // representations, nullable with its null propagation, checked with its throw — and the one
        // table applies it, exactly as it would the implicit form of the same conversion.
        if (context.SemanticHelper.GetOperation(cast) is IConversionOperation operation)
        {
            var operand = context.Converter.ConvertIr(cast.Expression);
            return TypedForTheChecker(ValueFlow.Apply(operation.GetConversion(), operation.Operand.Type, operation.Type,
                operation.ConstantValue.HasValue ? operation.ConstantValue.Value : null,
                operation.Operand.ConstantValue.HasValue ? operation.Operand.ConstantValue.Value : null,
                operand, context, operation.IsChecked || (operation.GetConversion().IsTupleConversion && ArithmeticContext.ChecksOverflow(cast, context))),
                operation, context);
        }

        // A cast through a user-defined operator with a standard conversion beside it is no operation of
        // its own: the bound tree holds the two conversions around the operand, where ValueFlow settles
        // them, and asks nothing of the cast (`(long)new Count(5)` over an operator to an int, #970).
        var inner = context.Converter.ConvertIr(cast.Expression);
        if (context.SemanticHelper.GetOperation(cast.Expression)?.Parent is IConversionOperation { IsImplicit: true })
            return inner;

        // No bound tree (a rewritten node, a model-less world): the SPELLED type decides, with the
        // same masks (IntegerWidth) over a truncation — the operand's type is unknowable here, so
        // the truncation stays even for sources that would not need it.
        var text = JsExprWriter.WriteIn(inner, JsPrecedence.Call);
        return cast.Type.ValueText() switch
        {
            "char" => JsExpr.Callish($"String.fromCharCode({text})"),
            "string" => JsExpr.Callish($"String({text})"),
            "sbyte" => IntegerWidth.Wrap(Truncate(text), (8, false)),
            "byte" => IntegerWidth.Wrap(Truncate(text), (8, true)),
            "short" => IntegerWidth.Wrap(Truncate(text), (16, false)),
            "ushort" => IntegerWidth.Wrap(Truncate(text), (16, true)),
            "int" => IntegerWidth.Wrap(Truncate(text), (32, false)),
            "uint" => IntegerWidth.Wrap(Truncate(text), (32, true)),
            // 64-bit has no plain-number wrap; without a model the truncation is all there is.
            "long" or "ulong" => Truncate(text),
            // Default passthrough for other types (compile-time assertion)
            _ => inner,
        };
    }

    /// <summary>
    /// A DOWNCAST (an explicit reference conversion into a class) changes nothing at run time, so the
    /// value passes as it is. The checker still reads the operand as its declared type, and a member
    /// only the target has was "Property does not exist on type" (#1303): in TypeScript the cast
    /// keeps its type as an assertion, the target named by its twin and imported as any reference to
    /// it is. A generic target is left as it was, as a list's generic item is left to inference, and
    /// so is a type with no twin to name, one the build keeps on the server included, whichever class
    /// declares the member the cast reaches. JavaScript has
    /// no assertion and gets the value alone, and the bundler drops an import used only as a type, so
    /// a base that casts to its own subclass meets no import cycle. The assertion does not CHECK: a
    /// wrong downcast reads what the object holds instead of throwing .NET's InvalidCastException,
    /// as it did before.
    /// </summary>
    private static JsExpr TypedForTheChecker(JsExpr value, IConversionOperation operation, ConversionContext context)
    {
        if (!context.TypeAnnotations) return value;
        var conversion = operation.GetConversion();
        if (!conversion.IsReference || conversion.IsImplicit) return value;
        if (operation.Type is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsGenericType: false } target) return value;
        if (!target.Locations.Any(location => location.IsInSource) && !target.IsRuntimeProvided()) return value;
        // A type with no twin to name writes the value alone, as it did, and the fence stays where the code
        // reads it. Which type has none is asked of its kind: an app's type the build writes no module for,
        // its base kept on the server included (IsKeptOut), and a type of the runtime that is itself the
        // server's. A runtime type over a base the server keeps (`Pressable : SingleChildNode`, `Row :
        // FlexNode`) has a twin of its own, and read as kept out, its cast was dropped, so a member it
        // inherits was read off the static type, which tsc refused (#1396).
        if (target.IsRuntimeProvided() ? StaysOnTheServer(target) : target.IsKeptOut()) return value;
        return JsExpr.Template("({0} as " + target.IntroduceTwin(context) + ")", value);
    }

    /// <summary>Whether a type of the runtime is the server's own: marked <c>[ServerOnly]</c> itself, or
    /// declared inside a type that is, which the runtime carries no twin of either, by the attribute or by its
    /// name where the attribute binds to an error type. Its base is no part of the question: the runtime's
    /// twin of a node stands without the server's base.</summary>
    private static bool StaysOnTheServer(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
            if (current.GetAttributes().Any(attribute => attribute.AttributeClass?.Name is "ServerOnlyAttribute" or "ServerOnly"))
                return true;
        return false;
    }

    /// <summary>The integer part of a value only spelled, never bound — Math.trunc as text.</summary>
    private static JsExpr Truncate(string text) => JsExpr.Callish($"Math.trunc({text})");

    private static ITypeSymbol? UnwrapNullable(ITypeSymbol? type)
        => type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            ? named.TypeArguments[0]
            : type;

    private static bool IsIntegral(ITypeSymbol? type) => type?.SpecialType is
        SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_Int16 or
        SpecialType.System_Byte or SpecialType.System_SByte or SpecialType.System_UInt16 or
        SpecialType.System_UInt32 or SpecialType.System_UInt64 or SpecialType.System_IntPtr or SpecialType.System_UIntPtr;

    private static bool IsNumeric(ITypeSymbol type) => IsIntegral(type)
        || type.SpecialType is SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal
            or SpecialType.System_Char;

    public int Priority => 10;
}
