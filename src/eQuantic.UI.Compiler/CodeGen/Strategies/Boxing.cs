using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.CodeGen.Strategies.Types;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// A value C# holds as <c>object</c> whose .NET type the browser cannot read from the value itself (#552, #742,
/// #768, #954). An enum member is its key (or a flags enum's number) and a float a number there, so a boxing
/// conversion of either boxes it with its type (<c>utils/box.ts</c>), and an unboxing asks the box. A list and an
/// array are one JavaScript array with a value tuple, so the conversion of either to <c>object</c> marks it as
/// compared by reference (<c>utils/marks.ts</c>). Read by <see cref="ValueFlow.Apply"/>, where every conversion
/// the bound tree reports is applied, and by the patterns and the casts that unbox.
/// <para>
/// An int and a double, a char and a string, a long and a ulong stay unboxed: boxing every number would cost
/// every <c>object</c> of an app, and what tells them apart is a limit the wiki states.
/// </para>
/// </summary>
internal static class Boxing
{
    /// <summary>The .NET type name a box carries, which an unboxing and an equality compare.</summary>
    internal static string NameOf(ITypeSymbol type) => type switch
    {
        { SpecialType: SpecialType.System_Single } => "System.Single",
        INamedTypeSymbol named => DefaultText.FullName(named),
        _ => type.ToDisplayString(),
    };

    /// <summary>Whether a value of <paramref name="type"/> is boxed with its type: an enum or a float, or a
    /// nullable one, whose value boxes as the underlying type's.</summary>
    internal static bool Carries(ITypeSymbol? type) =>
        type?.UnwrapNullable() is { SpecialType: SpecialType.System_Single } or INamedTypeSymbol { TypeKind: TypeKind.Enum };

    /// <summary>Whether <paramref name="type"/> is one a value is boxed into: <c>object</c> or <c>ValueType</c>. An
    /// interface and <c>Enum</c> keep the value as it is: their calls (<c>HasFlag(Enum)</c>, <c>CompareTo</c>)
    /// are the value's own lowerings, which read it as the browser holds it.</summary>
    internal static bool IsABox(ITypeSymbol? type) =>
        type?.SpecialType is SpecialType.System_Object or SpecialType.System_ValueType;

    /// <summary>
    /// Whether a boxing is an argument a call's own lowering reads by the value's type, which the box would hide
    /// from it: <c>Convert</c>'s <c>object</c> overloads, which read an enum and a nullable as they are written, and
    /// <c>CompareTo(object)</c>, which compares a member by its value. What they answer was already .NET's.
    /// </summary>
    internal static bool ReadThrough(IConversionOperation conversion) =>
        conversion.Parent is IArgumentOperation { Parent: IInvocationOperation { TargetMethod: var method } }
        && (method.ContainingType.ToDisplayString() == "System.Convert" || method.Name == "CompareTo");

    /// <summary>Whether a conversion is an argument of <c>object</c>'s statics (<c>Equals</c>, <c>ReferenceEquals</c>),
    /// which compare a list by the type it had before the conversion: a mark would change nothing there.</summary>
    internal static bool PassesThrough(IConversionOperation conversion) =>
        conversion.Parent is IArgumentOperation { Parent: IInvocationOperation { TargetMethod: { IsStatic: true, ContainingType.SpecialType: SpecialType.System_Object } } };

    /// <summary><c>$eq.box(value, type[, shape])</c>: the boxing of an enum member or a float.</summary>
    internal static JsExpr Box(ITypeSymbol type, JsExpr value, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        var underlying = type.UnwrapNullable() ?? type;
        var name = JsExpr.Literal(JsStringLiteral.Quote(NameOf(underlying)));
        return underlying is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType
            ? JsExpr.Call(JsExpr.Identifier(Eq.Box), value, name, JsExpr.Literal(EnumShape.Of(enumType)))
            : JsExpr.Call(JsExpr.Identifier(Eq.Box), value, name);
    }

    /// <summary>
    /// The unboxing to <paramref name="type"/>: an enum asks the box for its member, and a primitive value type
    /// (a number, a bool, a char) for what a box of its type holds, a box of any other type refused. Null where
    /// the target is neither, which a box never reaches.
    /// </summary>
    internal static JsExpr? Unbox(ITypeSymbol? type, JsExpr value, ConversionContext context)
    {
        var underlying = type?.UnwrapNullable();
        var nullable = type is not null && !ReferenceEquals(underlying, type);
        if (underlying is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
        {
            context.UsedHelpers.Add(Eq.Import);
            var shape = JsExpr.Literal(EnumShape.Of(enumType));
            var name = JsExpr.Literal(JsStringLiteral.Quote(NameOf(enumType)));
            // A nullable target lets null through, and unboxes a value.
            return nullable
                ? JsExpr.Call(JsExpr.Identifier(Eq.EnumUnbox), value, shape, name, JsExpr.Literal("true"))
                : JsExpr.Call(JsExpr.Identifier(Eq.EnumUnbox), value, shape, name);
        }
        if (underlying?.SpecialType is not (SpecialType.System_Boolean or SpecialType.System_Char or SpecialType.System_SByte
            or SpecialType.System_Byte or SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32
            or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Single
            or SpecialType.System_Double or SpecialType.System_Decimal))
            return null;
        context.UsedHelpers.Add(Eq.Import);
        return nullable
            ? JsExpr.Call(JsExpr.Identifier(Eq.Unbox), value, JsExpr.Literal(JsStringLiteral.Quote(NameOf(underlying))), JsExpr.Literal("true"))
            : JsExpr.Call(JsExpr.Identifier(Eq.Unbox), value, JsExpr.Literal(JsStringLiteral.Quote(NameOf(underlying))));
    }

    /// <summary>
    /// Whether a value of <paramref name="from"/> converted to <c>object</c> is a list or an array, marked as
    /// compared by reference: an array, a <c>List&lt;T&gt;</c>, or a collection interface, which holds one of
    /// them where it holds an array. A tuple implements no collection interface, so it is never marked.
    /// </summary>
    internal static bool IsMarked(ITypeSymbol? from, ITypeSymbol? to) =>
        to?.SpecialType == SpecialType.System_Object
        && (from is IArrayTypeSymbol
            || from is INamedTypeSymbol { OriginalDefinition: var definition } named
            && (definition.ToDisplayString() == "System.Collections.Generic.List<T>"
                || named.TypeKind == TypeKind.Interface
                && (named.SpecialType == SpecialType.System_Collections_IEnumerable
                    || named.AllInterfaces.Any(face => face.SpecialType == SpecialType.System_Collections_IEnumerable))));

    /// <summary>
    /// Whether a type test of <paramref name="typeSyntax"/> (<c>o is Tone</c>, <c>Tone t =&gt;</c>, <c>case float f:</c>)
    /// meets a value boxed here: the type is one a box carries and the pattern's input, as the bound tree types it,
    /// is <c>object</c> or <c>ValueType</c>. Over its own type, or a nullable one, the value is not boxed.
    /// </summary>
    internal static bool TestsABox(TypeSyntax typeSyntax, ConversionContext context)
    {
        var type = context.SemanticHelper.GetType(typeSyntax);
        if (!Carries(type) || type.IsNullableValue()) return false;
        var input = context.SemanticHelper.GetOperation(typeSyntax.Parent!) switch
        {
            IPatternOperation pattern => pattern.InputType,
            IIsTypeOperation test => test.ValueOperand.Type,
            _ => null,
        };
        return IsABox(input);
    }

    /// <summary><c>$eq.isBox(value, type)</c>: the test of a value held as <c>object</c> for an enum or a float.</summary>
    internal static string IsBox(TypeSyntax typeSyntax, string access, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        return $"{Eq.IsBox}({access}, {JsStringLiteral.Quote(NameOf(context.SemanticHelper.GetType(typeSyntax)!))})";
    }

    /// <summary>
    /// The type of the constant a pattern or a case label compares with, where it is an enum member or a float and
    /// the value it is compared with is held as <c>object</c>, as the pattern's input or the switch's value types
    /// it: that value is a box, and the constant is boxed to be compared with it. Null otherwise.
    /// </summary>
    internal static ITypeSymbol? BoxedConstant(IOperation? value)
    {
        var constant = value;
        while (constant is IConversionOperation conversion) constant = conversion.Operand;
        var input = value?.Parent switch
        {
            IPatternOperation pattern => pattern.InputType,
            ISingleValueCaseClauseOperation { Parent: ISwitchCaseOperation { Parent: ISwitchOperation over } } => over.Value.Type,
            _ => null,
        };
        return constant?.Type is { } type && Carries(type) && !type.IsNullableValue() && IsABox(input) ? type : null;
    }

    /// <summary><c>$eq.reference(value)</c>: the list or the array, marked.</summary>
    internal static JsExpr Mark(JsExpr value, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        return JsExpr.Call(JsExpr.Identifier(Eq.Reference), value);
    }
}
