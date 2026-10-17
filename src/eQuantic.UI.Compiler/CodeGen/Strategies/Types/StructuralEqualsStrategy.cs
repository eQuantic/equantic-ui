using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Routes <c>value.Equals(other)</c> on a record, a user struct, a value tuple or an anonymous object to
/// the structural equality its type compares by: <c>$eq.equals</c>, or, for a tuple and an anonymous
/// object, the comparison <see cref="ElementEquality"/> generates from their members' types. These are
/// plain objects and arrays at runtime, so the default <c>.equals(...)</c> emission would hit a missing
/// method: an anonymous object's threw <c>equals is not a function</c> (#743), as its type is a class and
/// not a structural value type. Gated strictly on the receiver's type (string keeps its own handling).
/// Priority 12, above the generic <see cref="Expressions.InvocationStrategy"/> fallback.
/// <para>
/// A tuple and an anonymous object are compared where the static types still say what the browser cannot:
/// the other value's type before C# boxed it into <c>Equals(object)</c>. One of another tuple or anonymous
/// type (other element or member types, other member names) is unequal before a member is read, as .NET's
/// <c>Equals</c> checks the type first, where the browser holds both as the same array or plain object. An
/// anonymous object's call is an instance call, which throws through null after its argument is evaluated
/// (<see cref="Eq.EqualsInstance"/>), where a tuple, a value, is never null and an empty <c>Nullable</c>
/// one equals only null.
/// </para>
/// <para>
/// The same static types decide <c>Equals(object)</c> inherited from <c>object</c> and the static
/// <c>object.Equals(a, b)</c>: by reference for an array, a list and a class that does not override
/// <c>Equals</c>, through an override wherever one exists, and the first operand's comparison before the
/// second's (<see cref="ConvertStaticObjectEquals"/>). A value held as <c>object</c> on both sides has lost
/// the type the comparison needs, and the runtime compares it by what it turns out to be
/// (<c>sameKey</c>): a list and an array are the same JavaScript array as a tuple there, and compare by
/// their elements as a tuple does (#909, #947).
/// </para>
/// </summary>
public class StructuralEqualsStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax inv)
            return false;
        if (inv.ArgumentList.Arguments.Count == 2)
            return IsStaticObjectEquals(inv, context);
        if (inv.ArgumentList.Arguments.Count != 1)
            return false;
        if (inv.Expression is IdentifierNameSyntax { Identifier.ValueText: "Equals" })
        {
            return context.SemanticHelper.GetSymbol(inv) is IMethodSymbol
            {
                IsStatic: false,
                Parameters.Length: 1,
                ContainingType.SpecialType: SpecialType.System_Object,
            } bare && !TwinMethodName.HoldsANameOfItsOwn(bare);
        }
        if (inv.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Equals" } ma)
            return false;
        if (context.SemanticHelper.GetSymbol(inv) is IMethodSymbol bound && TwinMethodName.HoldsANameOfItsOwn(bound))
            return false;
        // A bare `object` may hold a boxed value, and .NET compares boxed values by VALUE —
        // `((object)5).Equals((object)5)` is true. The helper already answers that for primitives.
        // A Nullable tuple's Equals is its value's, or, empty, whether the other value is null too, which
        // both comparisons answer for a null; its call went to an `equals` no array has.
        var receiver = context.SemanticHelper.GetType(ma.Expression);
        return receiver.IsStructuralValueType() || receiver is { IsAnonymousType: true }
            || receiver.UnwrapNullable() is INamedTypeSymbol { IsTupleType: true } or INamedTypeSymbol { TypeKind: TypeKind.Enum }
            || receiver?.SpecialType == SpecialType.System_Object
            || receiver?.TypeKind is TypeKind.Interface or TypeKind.TypeParameter
            || context.SemanticHelper.GetSymbol(inv) is IMethodSymbol
            {
                IsStatic: false,
                Parameters.Length: 1,
                ContainingType.SpecialType: SpecialType.System_Object,
            };
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var inv = (InvocationExpressionSyntax)node;
        context.UsedHelpers.Add(Eq.Import);

        if (inv.ArgumentList.Arguments.Count == 2)
            return ConvertStaticObjectEquals(inv, context);

        if (inv.Expression is IdentifierNameSyntax)
        {
            var arg = context.Converter.ConvertIr(inv.ArgumentList.Arguments[0].Expression);
            return JsExpr.Call(JsExpr.Identifier(Eq.SameItem), JsExpr.This, arg);
        }

        var ma = (MemberAccessExpressionSyntax)inv.Expression;
        if (ma.Expression is BaseExpressionSyntax)
        {
            var arg = context.Converter.ConvertIr(inv.ArgumentList.Arguments[0].Expression);
            var method = context.SemanticHelper.GetSymbol(inv) as IMethodSymbol;
            if (method?.ContainingType.Locations.Any(location => location.IsInSource) == true)
                return JsExpr.Call(JsExpr.Member(JsExpr.Identifier("super"), "equals"), arg);
            return method?.ContainingType.SpecialType == SpecialType.System_ValueType
                ? JsExpr.Call(JsExpr.Identifier(Eq.Equals), JsExpr.This, arg)
                : JsExpr.Binary(JsExpr.This, "===", arg);
        }

        var receiver = context.Converter.ConvertIr(ma.Expression);
        var other = context.Converter.ConvertIr(inv.ArgumentList.Arguments[0].Expression);
        var type = context.SemanticHelper.GetType(ma.Expression);

        if (type.UnwrapNullable() is INamedTypeSymbol named && (named.IsTupleType || named.IsAnonymousType))
        {
            // A tuple's Equals compares each element with that element type's default comparer, an array
            // by reference, which only the static type says: a tuple is an array here (#554). An anonymous
            // object's compares its members so too, and takes only another of the same members in the same
            // order for one of its type.
            var another = OfAnotherType(inv, named, context);
            // An empty Nullable tuple equals only null, and one with a value nothing of another type: both
            // values are still evaluated, as the call's receiver and argument are.
            if (another && type.IsNullableValue())
                return JsExpr.Call(JsExpr.Identifier(Eq.LogicAnd), JsExpr.Binary(receiver, "==", JsExpr.Literal("null")),
                    JsExpr.Binary(other, "==", JsExpr.Literal("null")));
            var comparison = ElementEquality.OwnEquals(named);
            if (named.IsAnonymousType || another)
                return JsExpr.Call(JsExpr.Identifier(Eq.EqualsInstance), receiver, other,
                    another ? JsExpr.Literal("null") : JsExpr.Opaque(comparison!));
            return JsExpr.Call(comparison is null ? JsExpr.Identifier(Eq.Equals) : JsExpr.Opaque(comparison), receiver, other);
        }

        if (type.UnwrapNullable() is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
        {
            var another = OfAnotherType(inv, enumType, context);
            if (another && type.IsNullableValue())
                return JsExpr.Call(JsExpr.Identifier(Eq.LogicAnd), JsExpr.Binary(receiver, "==", JsExpr.Literal("null")),
                    JsExpr.Binary(other, "==", JsExpr.Literal("null")));
            if (another)
                return JsExpr.Call(JsExpr.Identifier(Eq.EqualsInstance), receiver, other, JsExpr.Literal("null"));
            return JsExpr.Call(JsExpr.Identifier(Eq.Equals), receiver, other);
        }

        if (type.IsStructuralValueType())
        {
            return type!.IsReferenceType
                ? JsExpr.Call(JsExpr.Identifier(Eq.EqualsInstance), receiver, other)
                : JsExpr.Call(JsExpr.Identifier(Eq.Equals), receiver, other);
        }

        if (DoesNotDecide(type))
        {
            // What the receiver holds is not in its type, but the value C# boxed into it may still be in
            // the call: `((object)list).Equals(other)` is a list's Equals.
            var held = Unboxed(context.SemanticHelper.GetOperation(inv) is IInvocationOperation { Instance: { } instance }
                ? instance
                : null);
            var argument = UnboxedType(inv, 0, context);
            if (!DoesNotDecide(held))
            {
                if (IsExact(held!) && argument is not null && IsIncompatibleWith(argument, held!, context))
                    return JsExpr.Call(JsExpr.Identifier(Eq.EqualsInstance), receiver, other, JsExpr.Literal("null"));
                return OfAType(held!) is { } known
                    ? JsExpr.Call(JsExpr.Identifier(Eq.EqualsInstance), receiver, other, Function(known))
                    : JsExpr.Call(JsExpr.Identifier(Eq.EqualsInstance), receiver, other);
            }
            var (function, afterOwn) = WithoutAType(argument);
            if (function is null)
                return JsExpr.Call(JsExpr.Identifier(Eq.EqualsInstance), receiver, other);
            if (!afterOwn)
                return JsExpr.Call(JsExpr.Identifier(Eq.EqualsInstance), receiver, other, Function(function));
            JsExpr a = JsExpr.Identifier("$a"), b = JsExpr.Identifier("$b");
            return JsExpr.Call(JsExpr.Identifier(Eq.EqualsInstance), receiver, other,
                JsExpr.Arrow(context.TypeAnnotations ? "$a: any, $b: any" : "$a, $b",
                    JsExpr.Binary(JsExpr.Call(JsExpr.Identifier(Eq.SameItem), a, b), "||", JsExpr.Call(Function(function), a, b))));
        }

        return JsExpr.Call(JsExpr.Identifier(Eq.EqualsInstance), receiver, other, JsExpr.Identifier(Eq.SameItem));
    }

    private static bool IsStaticObjectEquals(InvocationExpressionSyntax invocation, ConversionContext context)
    {
        var isEqualsName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Equals" } => true,
            IdentifierNameSyntax { Identifier.ValueText: "Equals" } => true,
            _ => false,
        };
        if (!isEqualsName) return false;
        if (context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol method)
            return method is { IsStatic: true, ContainingType.SpecialType: SpecialType.System_Object };
        return invocation.Expression is MemberAccessExpressionSyntax
        {
            Expression: PredefinedTypeSyntax { Keyword.ValueText: "object" }
                or IdentifierNameSyntax { Identifier.ValueText: "Object" }
                or MemberAccessExpressionSyntax
                {
                    Expression: IdentifierNameSyntax { Identifier.ValueText: "System" },
                    Name.Identifier.ValueText: "Object",
                },
        };
    }

    /// <summary>
    /// The static <c>object.Equals(objA, objB)</c>, as .NET answers it: the same reference or two nulls,
    /// then <c>objA.Equals(objB)</c>, the FIRST operand's own Equals, whatever the second one is. The
    /// comparison is the one <c>objA</c>'s type before C# boxed it compares by (<see cref="OfAType"/>), and
    /// <c>objB</c>'s type says something only where <c>objA</c>'s says nothing (<see cref="WithoutAType"/>):
    /// a comparison chosen from the second operand skipped the first one's override (Copilot's review of
    /// #947). A value of a type that refuses every other (<see cref="IsExact"/>) beside a value of another
    /// type is unequal before either is read, as .NET's Equals checks the type first, where the browser
    /// holds a byte and an int, a char and a string, or a tuple and an array alike: both are still
    /// evaluated, and two nulls are still equal. The operands are the PARAMETERS', so
    /// <c>Equals(objB: x, objA: y)</c> asks <c>y</c>, and each is converted where it is written, which the
    /// template keeps as C#'s evaluation order.
    /// </summary>
    private static JsExpr ConvertStaticObjectEquals(InvocationExpressionSyntax invocation, ConversionContext context)
    {
        var parts = invocation.ArgumentList.Arguments.Select(argument => context.Converter.ConvertIr(argument.Expression)).ToArray();
        var (atA, typeA) = Operand(invocation, 0, context);
        var (atB, typeB) = Operand(invocation, 1, context);
        var a = $"{{{atA}}}";
        var b = $"{{{atB}}}";
        if (typeA is not null && typeB is not null && IsExact(typeA) && IsIncompatibleWith(typeB, typeA, context))
            return JsExpr.Template($"{Eq.LogicAnd}({a} == null, {b} == null)", parts);
        if (!DoesNotDecide(typeA))
            return JsExpr.Template($"{OfAType(typeA!) ?? Eq.Equals}({a}, {b})", parts);
        var (function, afterOwn) = WithoutAType(typeB);
        return JsExpr.Template(afterOwn
            ? $"({Eq.SameItem}({a}, {b}) || {function}({a}, {b}))"
            : $"{function ?? Eq.Equals}({a}, {b})", parts);
    }

    /// <summary>
    /// The operand of the static <c>object.Equals</c> that fills the parameter at <paramref name="ordinal"/>
    /// (<c>objA</c> is 0, <c>objB</c> is 1): which of the written arguments it is, a named argument read by
    /// its name, and its type before C# boxed it, null where nothing says.
    /// </summary>
    private static (int Written, ITypeSymbol? Type) Operand(InvocationExpressionSyntax invocation, int ordinal,
        ConversionContext context)
    {
        var arguments = invocation.ArgumentList.Arguments;
        var parameters = (context.SemanticHelper.GetSymbol(invocation) as IMethodSymbol)?.Parameters;
        var written = ordinal;
        for (var at = 0; at < arguments.Count; at++)
        {
            var name = arguments[at].NameColon?.Name.Identifier.ValueText;
            var slot = name is null
                ? at
                : parameters?.FirstOrDefault(parameter => parameter.Name == name)?.Ordinal
                  ?? (name == "objB" ? 1 : name == "objA" ? 0 : at);
            if (slot == ordinal) written = at;
        }
        var value = context.SemanticHelper.GetOperation(invocation) is IInvocationOperation operation
            ? operation.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == ordinal)?.Value
            : null;
        return (written, Unboxed(value));
    }

    /// <summary>
    /// Whether a type says nothing of the Equals its value answers by: <c>object</c>, <c>ValueType</c>,
    /// <c>Enum</c>, an interface a tuple implements, a type parameter, <c>dynamic</c>, and no type at all
    /// (a <c>null</c>). An interface no tuple implements says the value is found by reference or by its own
    /// Equals (<see cref="ElementEquality"/>).
    /// </summary>
    private static bool DoesNotDecide(ITypeSymbol? type) =>
        type is null
        || type.SpecialType is SpecialType.System_Object or SpecialType.System_ValueType or SpecialType.System_Enum
        || type.TypeKind is TypeKind.TypeParameter or TypeKind.Dynamic or TypeKind.Error
        || type.TypeKind == TypeKind.Interface && ElementEquality.Of(type) == "'own'";

    /// <summary>
    /// The comparison a value of <paramref name="type"/> answers <c>Equals(object)</c> by, null for
    /// <c>$eq.equals</c>: a tuple's and an anonymous object's own (<see cref="ElementEquality.OwnEquals"/>),
    /// which take only one of their type, and otherwise <c>EqualityComparer&lt;T&gt;.Default</c>'s, which
    /// asks the value's own Equals first: by reference for an array, a list and a class that does not
    /// override Equals, and the override wherever one exists, a derived class's included (#909).
    /// </summary>
    private static string? OfAType(ITypeSymbol type)
    {
        if (type.UnwrapNullable() is INamedTypeSymbol { IsTupleType: true } or INamedTypeSymbol { IsAnonymousType: true })
            return ElementEquality.OwnEquals((INamedTypeSymbol)type.UnwrapNullable()!);
        var comparer = ElementEquality.Comparer(type);
        return comparer == Eq.Equals ? null : comparer;
    }

    /// <summary>
    /// The comparison <c>x.Equals(y)</c> answers by where x's type says nothing (<see cref="DoesNotDecide"/>),
    /// from y's type before C# boxed it: x's own Equals FIRST, which every runtime comparison asks before
    /// anything else, and then a tuple's or an anonymous object's own comparison for a y of that type, a
    /// generated one asked only after x's own (<c>AfterOwn</c>), so a class whose override accepts the
    /// tuple still answers; identity for a y compared by reference (an array, a list, a class that does not
    /// override Equals), which no value of another type equals but through its own Equals; and what x
    /// turns out to be otherwise (<c>sameKey</c>). Null for <c>$eq.equals</c>, which asks x's own Equals
    /// first too.
    /// </summary>
    private static (string? Function, bool AfterOwn) WithoutAType(ITypeSymbol? other)
    {
        if (other.UnwrapNullable() is INamedTypeSymbol { IsTupleType: true } or INamedTypeSymbol { IsAnonymousType: true })
            return ElementEquality.OwnEquals((INamedTypeSymbol)other.UnwrapNullable()!) is { } generated
                ? (generated, true)
                : (null, false);
        return other is not null && !DoesNotDecide(other) && ElementEquality.Comparer(other) == Eq.SameItem
            ? (Eq.SameItem, false)
            : (Eq.SameKey, false);
    }

    /// <summary>
    /// Whether a value of this type is of exactly this type, and its <c>Equals(object)</c> refuses a value of
    /// any other: a value type (a number, a char, a bool, an enum, a tuple, a date, a struct) whose Equals
    /// is .NET's own or a record struct's, a string, and an anonymous object. A class may hold a derived
    /// instance that overrides Equals, and a struct that overrides <c>Equals(object)</c> may accept anything.
    /// </summary>
    private static bool IsExact(ITypeSymbol type)
    {
        var value = type.UnwrapNullable() ?? type;
        if (value.SpecialType == SpecialType.System_String || value.IsAnonymousType) return true;
        if (!value.IsValueType || value.TypeKind == TypeKind.TypeParameter) return false;
        return !value.GetMembers("Equals").OfType<IMethodSymbol>().Any(method =>
            method is { IsOverride: true, IsImplicitlyDeclared: false, Parameters: [{ Type.SpecialType: SpecialType.System_Object }] }
            && method.Locations.Any(location => location.IsInSource));
    }

    /// <summary>A comparison by its name: one of the runtime's own, or the expression that generates one.</summary>
    private static JsExpr Function(string comparison) =>
        comparison is Eq.SameItem or Eq.SameKey or Eq.Equals ? JsExpr.Identifier(comparison) : JsExpr.Opaque(comparison);

    private static ITypeSymbol? UnboxedType(InvocationExpressionSyntax invocation, int index, ConversionContext context) =>
        context.SemanticHelper.GetOperation(invocation) is IInvocationOperation operation && index < operation.Arguments.Length
            ? Unboxed(operation.Arguments[index].Value)
            : null;

    /// <summary>A value's type before C# boxed it, read through the conversions that keep the value what it is
    /// (a boxing, a reference conversion, an identity, a cast to <c>object</c>), a <c>Nullable</c>'s its
    /// value's; null where it has none (a <c>null</c>).</summary>
    private static ITypeSymbol? Unboxed(IOperation? value)
    {
        while (value is IConversionOperation conversion
               && conversion.GetConversion() is { IsBoxing: true } or { IsReference: true } or { IsIdentity: true })
            value = conversion.Operand;
        return value?.Type.UnwrapNullable() ?? value?.Type;
    }

    private static bool IsIncompatibleWith(ITypeSymbol type, ITypeSymbol receiver, ConversionContext context)
    {
        if (context.SemanticModel is not { } model) return false;
        if (type.SpecialType is SpecialType.System_Object or SpecialType.System_ValueType or SpecialType.System_Enum
            || type.TypeKind is TypeKind.Interface or TypeKind.TypeParameter or TypeKind.Dynamic or TypeKind.Error)
            return false;
        return !model.Compilation.ClassifyCommonConversion(type, receiver.UnwrapNullable() ?? receiver).IsIdentity;
    }

    /// <summary>
    /// Whether the other value of <c>Equals(object)</c> is, by its type before C# boxed it, of a type no
    /// value of <paramref name="receiver"/> can be: a tuple or an anonymous object of other element or
    /// member types, or any other concrete type, which .NET finds unequal before it reads a member. Its
    /// type is read through the conversions that keep the value what it is (a boxing, a reference
    /// conversion, an identity, a cast to <c>object</c>), and a <c>Nullable</c> of the receiver's type is
    /// the receiver's. False where the type could hold one: <c>object</c>, <c>ValueType</c>, an interface,
    /// a type parameter, <c>dynamic</c>, a value with no type of its own (a <c>null</c>); and for the
    /// strongly typed <c>Equals(T)</c> of a tuple, whose argument C# has already converted to the
    /// receiver's type. The element names of a tuple are no part of its type, as .NET's closed type erases
    /// them: an identity conversion is the same type.
    /// </summary>
    private static bool OfAnotherType(InvocationExpressionSyntax invocation, INamedTypeSymbol receiver, ConversionContext context)
    {
        if (context.SemanticHelper.GetOperation(invocation) is not IInvocationOperation
            {
                TargetMethod.Parameters: [{ Type.SpecialType: SpecialType.System_Object }],
            })
            return false;
        return UnboxedType(invocation, 0, context) is { } type && IsIncompatibleWith(type, receiver, context);
    }

    public int Priority => 12;
}
