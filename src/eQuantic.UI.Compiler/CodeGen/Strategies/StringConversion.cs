using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// What C# does to a value on its way INTO a string — in <c>"a" + x</c> and in <c>$"{x}"</c> —
/// where JavaScript would do something else. The conversion is the same in both places, so it is
/// decided in one: a null is the empty string (JavaScript writes <c>null</c>), a bool is
/// <c>True</c>/<c>False</c> (JavaScript lowercases), a nullable value type follows its value or
/// the empty string, an enum is its member NAME, a value the browser holds as data is its record
/// text, a value tuple is <c>(1, a)</c> and an anonymous object <c>{ A = 1 }</c> (#762), and a
/// number and a date are written in the culture in force, as .NET writes them (#454): a
/// fraction with the culture's separator (<c>1,5</c> in pt-BR), a negative integer with its minus
/// sign (sv-SE's is U+2212), a float in its own digits, a date in its type's own pattern. An
/// unsigned integer and a char read the same in every culture, and are left as they are, as is a
/// string known to be non-null and an integer constant that is not negative.
/// </summary>
public static class StringConversion
{
    /// <summary>The operand as the string C# would make of it.</summary>
    public static JsExpr ToDotNetString(ExpressionSyntax operand, JsExpr converted, ConversionContext context)
    {
        if (operand is LiteralExpressionSyntax { RawKind: (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.NullLiteralExpression })
            return JsExpr.Literal("''");

        var type = context.SemanticHelper.GetType(operand);
        if (type is null) return converted;

        // A member written by name folds to the name it is.
        if (type is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
            return JsExpr.Callish(Invocation.ToStringStrategy.EnumNameLookup(enumType, operand, JsExprWriter.Write(converted), context));

        // A literal or an interpolated string is never null.
        if (type.SpecialType == SpecialType.System_String
            && operand is LiteralExpressionSyntax or InterpolatedStringExpressionSyntax)
            return converted;

        // An integer constant that is not negative is its digits in every culture.
        if (IsNonNegativeIntegerConstant(operand, context)) return converted;

        return Of(type, converted, context);
    }

    /// <summary>
    /// A value of <paramref name="type"/> as the string C# would make of it, the type alone deciding:
    /// what a concatenation applies once its operand's syntax has had its say, and what
    /// <c>string.Join</c> applies to each element of a sequence of that type (#441).
    /// </summary>
    internal static JsExpr Of(ITypeSymbol type, JsExpr converted, ConversionContext context)
    {
        // A value whose type writes its text, which the formatter cannot read from the value alone.
        if (OwnText(type, converted, context) is { } own) return own;

        var text = JsExprWriter.Write(converted);

        // A string that MAY be null reads as itself or as nothing — the cheapest faithful spelling.
        // Annotated `string?` says so; a string from code with no nullable context (annotation
        // None) has not said it cannot be. Only a `string` under nullable-enabled code has.
        if (type.SpecialType == SpecialType.System_String)
            return type.NullableAnnotation == NullableAnnotation.NotAnnotated
                ? converted
                : JsExpr.Binary(converted, "??", JsExpr.Literal("''"));

        var real = type.UnwrapNullable() ?? type;

        // A number or a date is its text in the culture in force, through the formatter that writes
        // every other number and date (#454): JavaScript's own string of it is invariant, so a
        // pt-BR page read `1.5` where the server had rendered `1,5`, and a float spelled the double
        // underneath. A null one writes nothing, which the formatter answers too.
        if (WritesInTheCulture(real))
        {
            context.UsedHelpers.Add(Eq.Import);
            var kind = FormatKind.OfText(real) is { } named ? $", undefined, undefined, '{named}'" : "";
            return JsExpr.Callish($"{Eq.Format}({text}, null{kind})");
        }

        if (!NeedsFormatting(type)) return converted;

        context.UsedHelpers.Add(Eq.Import);
        return JsExpr.Callish($"{Eq.Format}({text}, null)");
    }

    /// <summary>
    /// The text of a value whose TYPE writes it, where the formatter could not read it from the value:
    /// an enum's member name (a [Flags] one its set flags, a NULLABLE one nothing for null, #452), a
    /// value tuple's and an anonymous object's own text (#762), a data twin's record text (#518), and the
    /// type's name for a type whose text is <c>Object.ToString</c>'s (<see cref="TypeNameText"/>). What
    /// a concatenation writes for such a value, and what a tuple's element and a record's or an anonymous
    /// object's member is handed (<see cref="Printed"/>): one rule, so a value prints alike on its own and
    /// inside another. Null for a type whose value says its text: a string, a number, a date, a bool, a
    /// class whose twin writes its own.
    /// </summary>
    private static JsExpr? OwnText(ITypeSymbol type, JsExpr converted, ConversionContext context)
    {
        if (type.UnwrapNullable() is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
            return JsExpr.Callish(Types.EnumShape.Text(enumType, JsExprWriter.Write(converted), context));

        var real = type.UnwrapNullable() ?? type;

        // A value TUPLE is an array here and an ANONYMOUS object a plain object, whose own strings are
        // JavaScript's, `1,2` and `[object Object]`. Each reads as the text .NET writes, from what only
        // its static type knows: a tuple's element types, and an anonymous type's C# member names in
        // their declared order (#762). A null one, an empty Nullable tuple, writes nothing.
        if (real is INamedTypeSymbol { IsTupleType: true } tuple)
            return TupleText(tuple, converted, Eq.TupleText, context);
        if (real is INamedTypeSymbol { IsAnonymousType: true } anonymous)
            return AnonymousText(anonymous, converted, Eq.RecordText, context);

        // A value the browser holds as DATA (`[TwinIsData]`, `Color`) is a plain object, whose own
        // string is `[object Object]`. It reads as the record text .NET writes, from the members
        // .NET prints, and a null one as nothing. A member whose own text its type decides says so,
        // as RecordText writes it: a `Curve`'s points are floats, and JavaScript's digits for 0.2f
        // are the double's, 0.20000000298023224 (#518).
        if (real is INamedTypeSymbol data && data.TwinIsData())
            return RecordText(data, converted, Eq.RecordText, context);

        // A type whose text is its name, which only the static type can say: a null one writes nothing.
        return TypeNameText(real) is { } name
            ? JsExpr.Conditional(JsExpr.Binary(converted, "==", JsExpr.Literal("null")), JsExpr.Literal("''"),
                JsExpr.Literal(JsStringLiteral.Quote(name)))
            : null;
    }

    /// <summary>
    /// The text a value of the type writes where its text is <c>Object.ToString</c>'s, its runtime type's
    /// name (<see cref="ReflectionName"/>, <c>System.Int32[]</c>), and the browser holds it as a JavaScript
    /// value of its own, with no twin to write the name (<see cref="DefaultText"/> is the twin's): an array,
    /// a list, a dictionary, a set, a delegate. Their own text in JavaScript is <c>1,2</c>,
    /// <c>[object Object]</c> or a function's source. Null for a type that writes another text (one
    /// that declares or derives a <c>ToString</c>, or is <c>IFormattable</c>), for one whose twin writes its
    /// own (a type of the compilation, a delegate's excepted), and where the static type does not say the
    /// runtime type's name: <c>object</c>, an interface, a type parameter, <c>dynamic</c>, or a name that
    /// holds an anonymous type or a type parameter.
    /// </summary>
    private static string? TypeNameText(ITypeSymbol type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                return Nameable(array.ElementType) ? ReflectionName.Of(array) : null;
            case INamedTypeSymbol { TypeKind: TypeKind.Delegate } @delegate:
                return Nameable(@delegate) ? ReflectionName.Of(@delegate) : null;
            case INamedTypeSymbol { TypeKind: TypeKind.Class or TypeKind.Struct, SpecialType: SpecialType.None } named
                when !named.IsTupleType && !named.IsAnonymousType && !named.TwinIsData() && !named.IsNullableValue()
                    && !named.Locations.Any(location => location.IsInSource)
                    && !named.AllInterfaces.Any(face => face is { Name: "IFormattable", ContainingNamespace.Name: "System" })
                    && IsTheRuntimeType(named) && WritesItsName(named) && Nameable(named):
                return ReflectionName.Of(named);
            default:
                return null;
        }
    }

    /// <summary>
    /// Whether a value of the static type is of that type at run time, whose name is the one .NET writes: a
    /// struct, a sealed class, and a class of .NET's own that is not abstract (a list, a dictionary, a set),
    /// whose subclasses the browser never holds, as a class over a base the app does not declare has no twin
    /// (<see cref="DefaultText"/>). A class of another library may hold a subclass's value, whose name is
    /// another (a vocabulary node is a text or a column), and an abstract one always does.
    /// </summary>
    private static bool IsTheRuntimeType(INamedTypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Struct || type.IsSealed) return true;
        var space = type.ContainingNamespace?.ToDisplayString() ?? "";
        return !type.IsAbstract && (space == "System" || space.StartsWith("System.", StringComparison.Ordinal));
    }

    /// <summary>Whether a type's text is the name <c>Object.ToString</c> and <c>ValueType.ToString</c> write:
    /// no type below them declares a <c>ToString</c>.</summary>
    private static bool WritesItsName(INamedTypeSymbol type)
    {
        for (var at = type; at is not null && at.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType);
             at = at.BaseType)
        {
            if (at.GetMembers(nameof(object.ToString)).OfType<IMethodSymbol>()
                .Any(method => !method.IsStatic && method.Parameters.Length == 0))
                return false;
        }
        return true;
    }

    /// <summary>Whether <see cref="ReflectionName"/> writes the runtime type's name for this static type:
    /// nothing in it is a type parameter, <c>dynamic</c>, an anonymous type or a type that did not bind.</summary>
    private static bool Nameable(ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol array => Nameable(array.ElementType),
        INamedTypeSymbol { IsAnonymousType: true } or INamedTypeSymbol { TypeKind: TypeKind.Error } => false,
        INamedTypeSymbol named => named.TypeArguments.All(Nameable),
        _ => false,
    };

    /// <summary>
    /// The record text of a value the browser holds as data, written by <paramref name="helper"/> over
    /// the members .NET prints: <see cref="Eq.RecordText"/> for the text itself, and
    /// <see cref="Eq.RecordTextGroup"/> for the delegate a <c>ToString</c> method group is, which writes
    /// the same text of the value it was made with (#518).
    /// </summary>
    internal static JsExpr RecordText(INamedTypeSymbol data, JsExpr value, string helper, ConversionContext context) =>
        RecordText(helper, value, data.Name,
            data.PrintedMembers().Select(member => (member.Name, (ITypeSymbol?)TwinData.TypeOf(member))), context);

    /// <summary>
    /// An anonymous object's text, as the <c>ToString</c> C# writes for an anonymous type writes it: a
    /// record's text with no name, <c>{ R = 255, Name = a }</c> and <c>{ }</c>, its members under their C#
    /// names in the order they are declared, each as a concatenation writes it. <paramref name="helper"/>
    /// is <see cref="Eq.RecordText"/> for the text itself and <see cref="Eq.RecordTextGroup"/> for the
    /// delegate a <c>ToString</c> method group is.
    /// </summary>
    internal static JsExpr AnonymousText(INamedTypeSymbol anonymous, JsExpr value, string helper, ConversionContext context) =>
        RecordText(helper, value, "",
            anonymous.GetMembers().OfType<IPropertySymbol>().Select(member => (member.Name, (ITypeSymbol?)member.Type)), context);

    /// <summary>
    /// A value tuple's text, as <c>ValueTuple.ToString</c> writes it (<c>(1, a)</c>), through the runtime's
    /// tuple text, which writes each element as a concatenation writes it: from the element itself where
    /// its type says nothing more, and from what the compiler hands where it does (<see cref="Printed"/>),
    /// up to the last element that says anything. A tuple of eight elements or more is one array here,
    /// and <c>TupleElements</c> lists every one of them, as .NET writes them flat from its <c>Rest</c>.
    /// <paramref name="helper"/> is <see cref="Eq.TupleText"/> for the text itself and
    /// <see cref="Eq.TupleTextGroup"/> for the delegate a <c>ToString</c> method group is (#762).
    /// </summary>
    internal static JsExpr TupleText(INamedTypeSymbol tuple, JsExpr value, string helper, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        var elements = tuple.TupleElements.Select(element => Printed(element.Type, context)).ToList();
        var said = elements.FindLastIndex(element => element is not null) + 1;
        return said == 0
            ? JsExpr.Call(JsExpr.Identifier(helper), value)
            : JsExpr.Call(JsExpr.Identifier(helper), value,
                JsExpr.Array(elements.Take(said).Select(element => element ?? JsExpr.Literal("null")).ToList()));
    }

    /// <summary>
    /// A record's text, as its PrintMembers writes it (<c>Name { A = 1, B = x }</c>), through the
    /// runtime's record text, which reads each member under its twin's name: a record the browser holds
    /// as a class (its <c>toString</c>) and one it holds as data alike, the delegate of a data twin's
    /// <c>ToString</c> method group, which <paramref name="helper"/> names, and an anonymous object's text,
    /// whose <paramref name="name"/> is empty. Each member is written as a concatenation would write it,
    /// which the runtime's formatter answers from the value alone where its type says nothing more, and
    /// from what the compiler hands where it does (<see cref="Printed"/>). A member whose type is not known
    /// is written from its value. One call shape for all of them, <c>helper(value, 'Name', [members])</c>.
    /// </summary>
    internal static JsExpr RecordText(string helper, JsExpr value, string name,
        IEnumerable<(string Name, ITypeSymbol? Type)> members, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        JsExpr Member((string Name, ITypeSymbol? Type) member)
        {
            var label = JsExpr.Literal(JsStringLiteral.Quote(member.Name));
            return Printed(member.Type, context) is { } how ? JsExpr.Array([label, how]) : label;
        }
        return JsExpr.Call(JsExpr.Identifier(helper), value, JsExpr.Literal(JsStringLiteral.Quote(name)),
            JsExpr.Array(members.Select(Member).ToList()));
    }

    /// <summary>
    /// What the runtime is told of a record's member or a tuple's element whose type says what its value
    /// cannot (the runtime's <c>Printed</c>): a function of the value writing the text its type writes,
    /// where <see cref="Of"/> writes one the formatter could not read from the value (<see cref="OwnText"/>),
    /// so an element prints as the same value prints on its own; a number's kind where it writes its own
    /// digits (a float) or no sign on a zero (an integer); and null where the value says all there is.
    /// </summary>
    private static JsExpr? Printed(ITypeSymbol? type, ConversionContext context)
    {
        var real = type.UnwrapNullable() ?? type;
        if (real is null) return null;
        if (OwnText(type!, JsExpr.Identifier("$v"), context) is { } own)
            return JsExpr.Arrow(context.TypeAnnotations ? "$v: any" : "$v", own);
        return WritesInTheCulture(real) && FormatKind.OfText(real) is { } kind
            ? JsExpr.Literal($"'{kind}'")
            : null;
    }

    /// <summary>
    /// Whether a value of the type reaches a formatter as its TEXT: one whose type writes its text
    /// (<see cref="OwnText"/>), an enum's excepted, which formats itself: a value tuple, an anonymous object,
    /// a value the browser holds as data (<c>[TwinIsData]</c>, <c>Color</c>, <c>Curve</c>) and a type whose
    /// text is its name (an array, a list, a delegate), none of them <c>IFormattable</c>, so a format written
    /// for one is ignored and an alignment pads its <c>ToString</c>. An interpolation hole with a format or an
    /// alignment and an argument of <c>string.Format</c> hand the formatter that text, which it pads; handed
    /// the value, it wrote JavaScript's <c>1,2</c> and <c>[object Object]</c> (#762).
    /// </summary>
    internal static bool ReachesTheFormatterAsText(ITypeSymbol? type) =>
        type.UnwrapNullable() is { } real && real is not INamedTypeSymbol { TypeKind: TypeKind.Enum }
        && (real is INamedTypeSymbol { IsTupleType: true } or INamedTypeSymbol { IsAnonymousType: true }
            || real.TwinIsData() || TypeNameText(real) is not null);

    /// <summary>
    /// The method group <c>value.ToString</c> of a value whose type writes its text (<see cref="OwnText"/>):
    /// the delegate the call is, writing what the call writes of the value as it is when the delegate is
    /// made, read once there, and, for a reference, refused there when it is null (<see cref="Eq.Receiver"/>),
    /// as .NET refuses to make the delegate of a null one's method. A tuple, an anonymous object and a data
    /// value through their own groups, and an enum and a type whose text is its name through
    /// <see cref="Eq.TextGroup"/>, handed the text as a function of the value. A group bound to the value's
    /// own <c>toString</c> wrote JavaScript's text, <c>1,2</c> for an array and an enum's key (#913). Null for
    /// any other type, and for a <c>Nullable</c> one, whose <c>ToString</c> is <c>Nullable</c>'s.
    /// </summary>
    internal static JsExpr? TextGroup(ITypeSymbol type, JsExpr receiver, ConversionContext context)
    {
        if (type.IsNullableValue()) return null;
        var held = type.IsReferenceType ? JsExpr.Call(JsExpr.Identifier(Eq.Receiver), receiver) : receiver;
        if (type is INamedTypeSymbol { IsTupleType: true } tuple) return TupleText(tuple, held, Eq.TupleTextGroup, context);
        if (type is INamedTypeSymbol { IsAnonymousType: true } anonymous)
            return AnonymousText(anonymous, held, Eq.RecordTextGroup, context);
        if (type is INamedTypeSymbol data && data.TwinIsData()) return RecordText(data, held, Eq.RecordTextGroup, context);
        if (OwnText(type, JsExpr.Identifier("$v"), context) is { } own)
        {
            context.UsedHelpers.Add(Eq.Import);
            return JsExpr.Call(JsExpr.Identifier(Eq.TextGroup), held,
                JsExpr.Arrow(context.TypeAnnotations ? "$v: any" : "$v", own));
        }
        if (type.SpecialType == SpecialType.System_Object || type.TypeKind is TypeKind.Interface or TypeKind.TypeParameter)
        {
            context.UsedHelpers.Add(Eq.Import);
            return JsExpr.Call(JsExpr.Identifier(Eq.TextGroup),
                JsExpr.Call(JsExpr.Identifier(Eq.Receiver), receiver),
                JsExpr.Arrow(context.TypeAnnotations ? "$v: any" : "$v", JsExpr.Callish($"{Eq.Format}($v, null)")));
        }
        return null;
    }

    /// <summary>
    /// The text an instance <c>ToString()</c> writes for a receiver of a reference type, <c>object</c>, an
    /// interface or a type parameter: refused when it is null (<see cref="Eq.Receiver"/>), as .NET's call
    /// through null throws <c>NullReferenceException</c>, where a hole, a concatenation and
    /// <c>Convert.ToString</c> of a null write nothing (#912). Null for any other receiver, whose call the
    /// caller writes as it always did.
    /// </summary>
    internal static JsExpr? InstanceText(ExpressionSyntax receiverSyntax, ITypeSymbol? type, JsExpr receiver,
        ConversionContext context)
    {
        if (type is { IsReferenceType: true } && ReachesTheFormatterAsText(type))
        {
            context.UsedHelpers.Add(Eq.Import);
            return OwnText(type, JsExpr.Call(JsExpr.Identifier(Eq.Receiver), receiver), context);
        }
        if (type is { SpecialType: SpecialType.System_Object } or { TypeKind: TypeKind.Interface or TypeKind.TypeParameter })
        {
            context.UsedHelpers.Add(Eq.Import);
            var checkedReceiver = IsAlwaysNonNull(receiverSyntax, context)
                ? receiver
                : JsExpr.Call(JsExpr.Identifier(Eq.Receiver), receiver);
            return JsExpr.Callish($"{Eq.Format}({JsExprWriter.Write(checkedReceiver)}, null)");
        }
        // A string's ToString is the string itself, and through null the call throws as any instance call
        // does: the null-tolerant text a hole and a concatenation write answered "" (Copilot's review of #947).
        if (type is { SpecialType: SpecialType.System_String })
        {
            if (IsAlwaysNonNull(receiverSyntax, context)) return receiver;
            context.UsedHelpers.Add(Eq.Import);
            return JsExpr.Call(JsExpr.Identifier(Eq.Receiver), receiver);
        }
        if (type is { IsReferenceType: true, TypeKind: not TypeKind.Error } && type.SpecialType != SpecialType.System_String
            && !type.IsDate())
        {
            if (IsAlwaysNonNull(receiverSyntax, context))
                return JsExpr.Callish($"String({JsExprWriter.Write(receiver)})");
            context.UsedHelpers.Add(Eq.Import);
            var checkedReceiver = JsExpr.Call(JsExpr.Identifier(Eq.Receiver), receiver);
            return JsExpr.Callish($"String({JsExprWriter.Write(checkedReceiver)})");
        }
        return null;
    }

    /// <summary>
    /// The value's text, as its <c>ToString()</c> writes it: what a concatenation writes, and
    /// <c>String()</c> of what a concatenation leaves as it is, since a number on its own is not text.
    /// </summary>
    internal static JsExpr ToText(ExpressionSyntax operand, JsExpr converted, ConversionContext context)
    {
        var text = ToDotNetString(operand, converted, context);
        return ReferenceEquals(text, converted)
            && context.SemanticHelper.GetType(operand)?.SpecialType != SpecialType.System_String
            ? JsExpr.Callish($"String({JsExprWriter.Write(converted)})")
            : text;
    }

    private static bool IsAlwaysNonNull(ExpressionSyntax expression, ConversionContext context) =>
        (expression.Parent is MemberAccessExpressionSyntax access
            && context.SemanticHelper.Original(access) is MemberBindingExpressionSyntax)
        || expression switch
        {
            ParenthesizedExpressionSyntax parenthesized => IsAlwaysNonNull(parenthesized.Expression, context),
            BaseObjectCreationExpressionSyntax
                or AnonymousObjectCreationExpressionSyntax
                or ArrayCreationExpressionSyntax
                or ImplicitArrayCreationExpressionSyntax
                or CollectionExpressionSyntax
                or ThisExpressionSyntax
                or BaseExpressionSyntax
                or InterpolatedStringExpressionSyntax
                or LiteralExpressionSyntax { RawKind: (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression } => true,
            _ => false,
        };

    /// <summary>
    /// The value's text in the INVARIANT culture, as <c>ToString(CultureInfo.InvariantCulture)</c>
    /// writes it: a number or a date through the formatter in the invariant culture, and anything else
    /// as it reads in every culture.
    /// </summary>
    internal static JsExpr InvariantText(ExpressionSyntax operand, JsExpr converted, ConversionContext context)
    {
        var type = context.SemanticHelper.GetType(operand);
        var real = type.UnwrapNullable() ?? type;
        // A value held as an object, or as dynamic, may be a number or a date: the formatter reads it, in the
        // invariant culture, where its text was the page's (Copilot's review of #958).
        if (real is { SpecialType: SpecialType.System_Object } or { TypeKind: TypeKind.Dynamic })
        {
            context.UsedHelpers.Add(Eq.Import);
            return JsExpr.Callish($"{Eq.Format}({JsExprWriter.Write(converted)}, null, undefined, true)");
        }
        if (real is null || !WritesInTheCulture(real)) return ToText(operand, converted, context);
        context.UsedHelpers.Add(Eq.Import);
        var kind = FormatKind.OfText(real) is { } named ? $", '{named}'" : "";
        return JsExpr.Callish($"{Eq.Format}({JsExprWriter.Write(converted)}, null, undefined, true{kind})");
    }

    /// <summary>
    /// Whether the culture changes this type's text: a fraction's separator, a signed integer's
    /// minus sign and a date's patterns. An unsigned integer, a char and a TimeSpan (whose text is
    /// invariant in .NET) read the same everywhere.
    /// </summary>
    private static bool WritesInTheCulture(ITypeSymbol type) =>
        type.SpecialType is SpecialType.System_Double or SpecialType.System_Single
            or SpecialType.System_Decimal or SpecialType.System_SByte or SpecialType.System_Int16
            or SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_IntPtr
        || type.IsDate();

    /// <summary>An integer constant that is not negative: its digits are its text in every culture.</summary>
    private static bool IsNonNegativeIntegerConstant(ExpressionSyntax operand, ConversionContext context) =>
        context.SemanticHelper.TryGetConstantValue(operand, out var value)
        && value is sbyte and >= 0 or short and >= 0 or int and >= 0 or long and >= 0;

    /// <summary>Whether JavaScript's own string of this type differs from .NET's: booleans, anything
    /// nullable, and a bare <c>object</c> (which may hold either).</summary>
    private static bool NeedsFormatting(ITypeSymbol type) =>
        type.SpecialType is SpecialType.System_Boolean or SpecialType.System_Object
        || type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
        || (type.IsReferenceType && type.NullableAnnotation == NullableAnnotation.Annotated);
}
