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
/// <para>
/// A nullable struct compares as the lifted <c>==</c> does, two empty values equal and an empty one equal
/// to no value, where the bound call says so (#968): its <c>Equals</c>, which is its value's
/// <c>Equals(object)</c>, <c>Nullable.Equals&lt;T&gt;</c>, and <c>EqualityComparer&lt;T&gt;.Default.Equals</c>,
/// which over any type asks the comparison <see cref="ElementEquality"/> writes for its default comparer.
/// Every one of those comparisons finds a null equal only to a null. The build refused all three.
/// </para>
/// </summary>
public class StructuralEqualsStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax inv)
            return false;
        if (inv.ArgumentList.Arguments.Count == 2)
            return IsStaticObjectEquals(inv, context) || DefaultComparerOf(inv, context) is not null;
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
        var bound = context.SemanticHelper.GetSymbol(inv) as IMethodSymbol;
        // An interface's typed Equals is dispatched by its contract, whatever name its implementation holds.
        if (ElementEquality.ContractKey(bound) is not null)
            return true;
        if (bound is not null && TwinMethodName.HoldsANameOfItsOwn(bound))
            return false;
        // The member the call binds decides, never its receiver alone: object's Equals(object), whatever the
        // receiver, and IEquatable<T>'s, which generic code calls. An Equals an interface of the app's
        // declares is dispatched as any member of it is: taken here for the receiver's type, a call that
        // binds `IProbe.Equals(IProbe)` answered by identity and never ran the implementation (Copilot's
        // second review of #947).
        if (bound is { IsStatic: false, Parameters.Length: 1, ContainingType.SpecialType: SpecialType.System_Object }
            || ElementEquality.IsEquatableEquals(bound))
            return true;
        // A bare `object` may hold a boxed value, and .NET compares boxed values by VALUE —
        // `((object)5).Equals((object)5)` is true. The helper already answers that for primitives.
        // A Nullable tuple's Equals is its value's, or, empty, whether the other value is null too, which
        // both comparisons answer for a null; its call went to an `equals` no array has.
        var receiver = context.SemanticHelper.GetType(ma.Expression);
        // A nullable struct's Equals is Nullable<T>'s, which asks its value's Equals(object) (#968).
        return receiver.IsStructuralValueType() || receiver is { IsAnonymousType: true }
            || IsNullablesEquals(bound) && receiver.UnwrapNullable().IsStructuralValueType()
            || receiver.UnwrapNullable() is INamedTypeSymbol { IsTupleType: true } or INamedTypeSymbol { TypeKind: TypeKind.Enum }
            || bound is null && receiver?.SpecialType == SpecialType.System_Object;
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var inv = (InvocationExpressionSyntax)node;
        context.UsedHelpers.Add(Eq.Import);

        if (inv.ArgumentList.Arguments.Count == 2)
            return DefaultComparerOf(inv, context) is { } compared
                ? ConvertDefaultEquality(inv, compared, context)
                : ConvertStaticObjectEquals(inv, context);

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

        // An interface's typed Equals, IEquatable<T>'s or one the app's interface declares, through the contract
        // the call binds: the method the value's twin maps it to, and what the value turns out to be where it
        // answers none, a number and a string included. The method decides a null argument itself (Copilot's
        // second and third reviews of #947).
        if (ElementEquality.ContractKey(context.SemanticHelper.GetSymbol(inv) as IMethodSymbol) is { } contract)
            return JsExpr.Call(JsExpr.Identifier(Eq.EqualsInstance), receiver, other,
                JsExpr.Call(JsExpr.Identifier(Eq.EquatableCall), JsExpr.Literal(JsStringLiteral.Quote(contract))));

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

        // A nullable struct's is its value's, which both comparisons answer for an empty one: equal only to
        // null, as Nullable<T>.Equals(object) answers before it asks the value (#968).
        if (type.UnwrapNullable().IsStructuralValueType())
        {
            // A struct whose Equals(object) is the app's code is asked it, whatever the other value is, and
            // every time: `$eq.equals` refused a value of another JavaScript type before the override could run,
            // so a struct whose override takes a string answered false (Copilot's second review of #947), and
            // `sameItem` answered a value compared with itself, or with null, without asking it (Copilot's first
            // review of #977). A value is never null, so its own `equals` is called as C# calls it, a null
            // argument included; a nullable one answers a null first, as Nullable<T>.Equals(object) does.
            if (AsksItsOwnEquals(type))
                return type.IsNullableValue()
                    ? JsExpr.Call(JsExpr.Identifier(Eq.EqualsOwn), receiver, other)
                    : JsExpr.Call(JsExpr.Member(receiver, "equals"), other);
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

    /// <summary>Whether the call binds <c>Nullable&lt;T&gt;.Equals(object)</c>: its value's <c>Equals(object)</c>
    /// for a value, and whether the other value is null too for an empty one.</summary>
    private static bool IsNullablesEquals(IMethodSymbol? method) =>
        method is { Name: "Equals", IsStatic: false, Parameters: [{ Type.SpecialType: SpecialType.System_Object }] }
        && method.ContainingType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

    /// <summary>
    /// The type whose DEFAULT comparer a two-argument call asks, read from the bound call, or null for any other
    /// call: <c>Nullable.Equals&lt;T&gt;(T?, T?)</c>, which is <c>EqualityComparer&lt;T&gt;.Default</c> over two
    /// values and finds two empty ones equal, and <c>EqualityComparer&lt;T&gt;.Default.Equals(x, y)</c> itself, a
    /// call made on the <c>Default</c> property, never on another comparer (#968). The property is never
    /// read: no value the browser holds carries it.
    /// </summary>
    private static ITypeSymbol? DefaultComparerOf(InvocationExpressionSyntax invocation, ConversionContext context)
    {
        // Every call of two arguments reaches this gate: only one written as Equals is bound to ask, and the
        // bound call decides what it is.
        var written = invocation.Expression switch
        {
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            SimpleNameSyntax name => name.Identifier.ValueText,
            _ => null,
        };
        if (written != "Equals") return null;
        if (context.SemanticHelper.GetOperation(invocation) is not IInvocationOperation
            {
                TargetMethod: { Name: "Equals", Parameters.Length: 2 } method,
            } call)
            return null;
        if (method is { IsStatic: true, ContainingType: { Name: "Nullable", Arity: 0 } nullable, TypeArguments: [var value] }
            && nullable.ContainingNamespace?.ToDisplayString() == "System")
            return value;
        // EqualityComparer<T>'s OWN Default: a static property of the app's named alike, answering one, is a value its
        // getter answers, a throw or a null among them, and was dropped unread (Copilot's first review of #977).
        return method is { IsStatic: false, ContainingType: { Name: "EqualityComparer", Arity: 1 } comparer }
            && IsEqualityComparer(comparer)
            && call.Instance is IPropertyReferenceOperation { Property: { Name: "Default", IsStatic: true, ContainingType: var home } }
            && IsEqualityComparer(home)
                ? comparer.TypeArguments[0]
                : null;

        static bool IsEqualityComparer(INamedTypeSymbol type) =>
            type.OriginalDefinition is { Name: "EqualityComparer", Arity: 1 } definition
            && definition.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic";
    }

    /// <summary>
    /// A call that asks the default comparer of <paramref name="compared"/>: the comparison
    /// <see cref="ElementEquality"/> writes for it, which finds a null equal only to a null, over the two
    /// values as the PARAMETERS take them, each converted where it is written, which the template keeps as
    /// C#'s order of evaluation (#968).
    /// </summary>
    private static JsExpr ConvertDefaultEquality(InvocationExpressionSyntax invocation, ITypeSymbol compared,
        ConversionContext context)
    {
        var parts = invocation.ArgumentList.Arguments.Select(argument => context.Converter.ConvertIr(argument.Expression)).ToArray();
        var (atX, _) = Operand(invocation, 0, context);
        var (atY, _) = Operand(invocation, 1, context);
        var (function, otherwise) = DefaultComparison(compared);
        var third = otherwise is null ? "" : $", {otherwise}";
        return JsExpr.Template($"{function}({{{atX}}}, {{{atY}}}{third})", parts);
    }

    /// <summary>
    /// The function a call on the default comparer of <paramref name="compared"/> is: .NET's
    /// <c>GenericEqualityComparer</c>, <c>ObjectEqualityComparer</c> and <c>NullableEqualityComparer</c> ask the
    /// value's own equality with no identity check, so a value compared with itself asks it too, its side effects
    /// with it and a refusal of itself (Copilot's first and second reviews of #977).
    /// <list type="bullet">
    /// <item>generic code, where .NET settles the type at run time: <see cref="Eq.EqualsOwnGeneric"/>, the value's
    /// <c>IEquatable&lt;T&gt;</c> where its twin answers it, and its own <c>equals</c> otherwise;</item>
    /// <item>a type answering an <c>IEquatable&lt;T&gt;</c> of the app's: the comparison <see cref="ElementEquality"/>
    /// writes for it, which asks the typed method with no identity check;</item>
    /// <item>a struct or a class whose <c>Equals(object)</c> is the app's, declared or inherited from the app's
    /// base, and a type that cannot say what a value holds, <c>object</c> or an interface:
    /// <see cref="Eq.EqualsOwn"/>, the value's own <c>equals</c> where it carries one;</item>
    /// <item>any other type, whose equality C# or .NET writes and no one can tell from an identity check: its
    /// comparison, unchanged, a class with no override by reference.</item>
    /// </list>
    /// <c>Otherwise</c> is the comparison <see cref="Eq.EqualsOwn"/> compares a value with no <c>equals</c> of its own
    /// by, where that is not <see cref="Eq.SameKey"/>, its default: by reference behind an interface no tuple
    /// implements, as the type's own comparison compares it.
    /// </summary>
    private static (string Function, string? Otherwise) DefaultComparison(ITypeSymbol compared)
    {
        var value = compared.UnwrapNullable() ?? compared;
        if (value is ITypeParameterSymbol) return (Eq.EqualsOwnGeneric, null);
        var comparison = ElementEquality.Comparer(compared);
        if (ElementEquality.SelfContract(value) is not null) return (comparison, null);
        if (OwnsObjectEquals(value)) return (Eq.EqualsOwn, null);
        if (CannotSay(value)) return (Eq.EqualsOwn, comparison == Eq.SameKey ? null : comparison);
        return (comparison, null);
    }

    /// <summary>Whether a struct's or a class's <c>Equals(object)</c> is the app's code: an override the struct
    /// declares, or the most derived override along the class's chain, declared or inherited, written in the
    /// app's source.</summary>
    private static bool OwnsObjectEquals(ITypeSymbol value)
    {
        if (value.IsValueType) return DeclaresObjectEquals(value);
        if (value is not INamedTypeSymbol { TypeKind: TypeKind.Class }) return false;
        for (var at = (INamedTypeSymbol?)value; at is not null && at.SpecialType != SpecialType.System_Object; at = at.BaseType)
        {
            if (at.GetMembers("Equals").OfType<IMethodSymbol>().FirstOrDefault(method =>
                    method is { IsOverride: true, Parameters: [{ Type.SpecialType: SpecialType.System_Object }] }) is { } own)
                return !own.IsImplicitlyDeclared && own.Locations.Any(location => location.IsInSource);
        }
        return false;
    }

    /// <summary>Whether a type cannot say what its value holds: <c>object</c>, <c>ValueType</c>, <c>Enum</c> and an
    /// interface, behind which a value whose equality the app wrote may stand.</summary>
    private static bool CannotSay(ITypeSymbol value) =>
        value.SpecialType is SpecialType.System_Object or SpecialType.System_ValueType or SpecialType.System_Enum
        || value.TypeKind == TypeKind.Interface;

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
        // A struct's own Equals(object) override, before anything reads the other value.
        if (DeclaresObjectEquals(type)) return Eq.SameItem;
        // Equals(object), which never asks IEquatable<T>: a typed overload answers only a call bound to it.
        var comparer = ElementEquality.Comparer(type, equatable: false);
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
        return other is not null && !DoesNotDecide(other) && ElementEquality.Comparer(other, equatable: false) == Eq.SameItem
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
        return !DeclaresObjectEquals(value);
    }

    /// <summary>
    /// Whether a STRUCT's <c>Equals(object)</c> runs the app's code, which .NET asks every time a value is compared,
    /// itself included: an override the struct declares, or a record struct's synthesized one, which calls the
    /// typed <c>Equals</c> the app wrote. A struct whose equality C# or .NET writes compares by its members, which
    /// no one can tell apart from an identity check, and a record class keeps the one its synthesized
    /// <c>Equals</c> begins with.
    /// </summary>
    private static bool AsksItsOwnEquals(ITypeSymbol? type) =>
        type.UnwrapNullable() is INamedTypeSymbol { IsValueType: true } value
        && (DeclaresObjectEquals(value) || value.IsRecord && ElementEquality.SelfMethod(value) is not null);

    /// <summary>
    /// Whether a value type of the app's own (or a project it references) overrides <c>Equals(object)</c>,
    /// which then answers for any other value, of any type: what a <c>struct</c> declares, never what C#
    /// synthesizes for a <c>record struct</c>, and never .NET's own, whose types refuse every other.
    /// </summary>
    private static bool DeclaresObjectEquals(ITypeSymbol type)
    {
        var value = type.UnwrapNullable() ?? type;
        return value.IsValueType && value.GetMembers("Equals").OfType<IMethodSymbol>().Any(method =>
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
