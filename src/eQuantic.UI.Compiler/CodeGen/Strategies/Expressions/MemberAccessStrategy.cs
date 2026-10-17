using eQuantic.UI.Compiler.CodeGen.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// The fallback for <c>receiver.Member</c> once every dedicated strategy has declined: a handful of
/// well-known statics (<c>DateTime.Now</c>, <c>Guid.Empty</c>), the type-dependent <c>Count</c>
/// (<c>size</c> on a Set, <c>length</c> on a sequence; a dictionary's is DictionaryStrategy's),
/// C# 14 extension properties lowered to their static home, and otherwise the camelCased member
/// on the converted receiver — with a method group bound to that receiver.
/// </summary>
public class MemberAccessStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is MemberAccessExpressionSyntax;
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)node;
        var name = memberAccess.Name.Identifier.ValueText;
        // A nested type of the app's named through its owner (`Outer.Inner`) is its twin (#584), never a
        // member of the owner's.
        if (context.SemanticHelper.GetSymbol(node) is INamedTypeSymbol keptOut && keptOut.ReportIfKeptOut(node, context))
            return JsExpr.Identifier("undefined");
        if (context.SemanticHelper.GetSymbol(node) is INamedTypeSymbol nested && nested.NestedTwinName() is { } twin)
        {
            nested.RegisterIntroduced(context);
            return JsExpr.Identifier(twin);
        }
        var receiver = context.Converter.ConvertIr(memberAccess.Expression);
        // The receiver's text, fenced for receiver position — what the template branches splice.
        var expr = JsExprWriter.WriteIn(receiver, JsPrecedence.Call);

        // Convert C# properties to JS
        // Note: Specialized mappings (HasValue, Value) are handled by NullableStrategy

        // Semantic check for DateTime.Now, Guid.Empty, etc.
        var symbol = context.SemanticHelper.GetSymbol(node);

        // C# 14 extension PROPERTY (`sequence.IsEmpty` from an extension block): the emitter
        // lowers it to a static call on the declaring class with the receiver as the argument —
        // the read follows it there. Static extension properties take no receiver.
        if (symbol is IPropertySymbol && symbol.ExtensionBlockHome() is { } extensionHome)
        {
            extensionHome.RegisterIntroduced(context);
            var home = JsExpr.Member(JsExpr.Identifier(extensionHome.Name), name.ToCamelCase());
            return symbol.IsStatic ? JsExpr.Call(home) : JsExpr.Call(home, receiver);
        }

        // The HOST-ONLY fence. `FaceResolution.Unresolved` is a read, not a call, and a fence that
        // guards calls and not reads reads as protection while being none — the emitted
        // `FaceResolution.unresolved` names an export the runtime does not have, and the page dies
        // at hydration with SSR still answering 200.
        // Through the RECEIVER's type, so a host-only BASE fences itself without fencing the
        // inherited members of the client-visible nodes under it (SingleChildNode.Child, #162).
        // Refused, the read is written as nothing more, and a read made on it is the same refusal:
        // the read fence below told `Path.DirectorySeparatorChar` again as untranslated (EQ2004),
        // beside the EQ2112 that names its API, as the other sites of the fence never do (#816).
        if (symbol is not null
            && symbol.ReportIfHostOnly(node, context, context.SemanticHelper.GetType(memberAccess.Expression)))
        {
            context.RefusedReads.Add(context.SemanticHelper.Original(node));
            return JsExpr.Literal("undefined");
        }

        if (symbol != null)
        {
            var containingType = symbol.ContainingType.ToDisplayString();
            if (containingType == "System.DateTime" && (symbol.Name == "Now" || symbol.Name == "Today"))
            {
                return JsExpr.Callish("new Date()");
            }
            if (containingType == "System.Guid" && symbol.Name == "Empty")
            {
                return JsExpr.Literal("''");
            }
        }

        // Heuristic fallback
        if (expr == "DateTime" && (name == "Now" || name == "Today")) return JsExpr.Callish("new Date()");
        if (expr == "Guid" && name == "Empty") return JsExpr.Literal("''");
        if ((expr == "string" || expr == "String") && name == "Empty") return JsExpr.Literal("''");

        // .Count is type-dependent, and one table answers it for a member access and a pattern alike. A
        // FIELD called Count is no collection's: it is read in its slot below, as every field is, where the
        // table answered `count`, the method a case apart from it (#396).
        if (name == "Count" && symbol is not IFieldSymbol)
            return CountSpelling.Read(receiver, context.SemanticHelper.GetType(memberAccess.Expression), context);

        // The camelCase guess below is exactly the invocation fallback's story (EQ2006): an
        // in-tree access an AUTHORITATIVE model could not bind is missing references or code that
        // doesn't compile — before this, an unresolved PascalCase access could even fall into the
        // enum shape-heuristic and ship as a member-name string. Guessing stays legal where it is
        // honest: snippets, rewritten nodes, non-authoritative hosts.
        if (symbol is null && !context.CanGuess(node))
        {
            context.Report(node, ConversionSeverity.Error, "EQ2006",
                $"'{memberAccess.Name.Identifier.ValueText}' does not bind in the compiler's semantic model, "
                + "so any translation would be a guess. Either this code does not compile, or the "
                + "compiler is missing references/generated sources — the SDK passes them via "
                + "--refs/--generated; a custom host must do the same.");
        }

        // A .NET member no value in the browser carries is refused, as a .NET method the invocation
        // fallback reaches is (#847).
        PlatformMemberRead.ReportIfUntranslated(symbol, node, context);

        name = name switch
        {
            "Length" => "length",
            "Count" => "length", // Arrays/Lists (fallback when type is unknown)
            // A method by the name it holds on its twin, which a method that hides one has of its own (#563).
            _ when symbol is IMethodSymbol bound => TwinMethodName.Of(bound, name),
            _ => name.ToCamelCase()
        };

        if (string.IsNullOrEmpty(name)) return receiver;

        // A field is read in its slot, which moves a case apart from another member (#396).
        if (symbol is IFieldSymbol field) name = field.TwinSlot();

        var member = JsExpr.Member(receiver, name);

        // A method REFERENCE (not being called) is a method group: bind it to its receiver.
        if (symbol is IMethodSymbol method)
        {
            var isDirectInvocation = memberAccess.Parent is InvocationExpressionSyntax invocation &&
                                  invocation.Expression == memberAccess;
            if (!isDirectInvocation)
            {
                // `base.M` is the base's method called on THIS object, with no virtual dispatch, and
                // `super` is not a value JavaScript lets anything be passed: `super.m.bind(super)`
                // failed the whole module at parse.
                if (memberAccess.Expression is BaseExpressionSyntax)
                    return JsExpr.Call(JsExpr.Member(member, "bind"), JsExpr.This);
                // An extension's method lives on the home its call goes to, so the group binds the
                // receiver there, read once: `Ext.twice.bind(Ext, s)`. Bound to the receiver itself,
                // it named a member the receiver never had, and making the delegate threw.
                if (InvocationStrategy.ExtensionHome(method, node, context) is { } extension)
                {
                    var homeClass = JsExpr.Identifier(extension.Home.Name);
                    var bind = JsExpr.Member(JsExpr.Member(homeClass, name), "bind");
                    return extension.TakesReceiver ? JsExpr.Call(bind, homeClass, receiver) : JsExpr.Call(bind, homeClass);
                }
                // A RECORD member of a value the browser holds as DATA is not on its companion, which
                // holds the type's methods and its presets: `Equals` bound there named a member it has
                // not, and making the delegate threw, and `ToString` bound the object's own
                // `toString`, which answered `[object Object]`. Each answers through the helper its
                // call uses.
                if (RecordMemberGroup(method, memberAccess, receiver, context) is { } recordGroup)
                    return recordGroup;
                // A method of a value the browser holds as DATA lives on its companion, value first,
                // so the group binds the value there, read once, as C# copies the receiver into the
                // delegate when it is made: `Color.withOpacity.bind(Color, value)`.
                if (!method.IsStatic && method.ContainingType is { } dataType && dataType.TwinIsData())
                {
                    dataType.RegisterIntroduced(context);
                    var home = JsExpr.Identifier(dataType.Name);
                    return JsExpr.Call(JsExpr.Member(JsExpr.Member(home, name), "bind"), home, receiver);
                }
                // The receiver is read ONCE, as C# reads it when the delegate is made: written twice,
                // a receiver that is a call ran twice (`make().value.bind(make())`, #619). The
                // template binds a part used twice and inlines a plain name (`this.value.bind(this)`).
                return JsExpr.Template($"{{0}}.{name}.bind({{0}})", receiver);
            }
        }

        return member;
    }

    /// <summary>
    /// The method group of a record member of a value the browser holds as DATA (<c>[TwinIsData]</c>,
    /// <c>Color</c> and <c>Curve</c>), of a value tuple's or an anonymous object's <c>Equals</c>, and the
    /// <c>ToString</c> of every value whose type writes its text: the delegate its call is, made by the
    /// runtime as <c>$eq.hash.group</c> makes <c>GetHashCode</c>'s. <c>Equals</c> compares as the call
    /// compares (<c>StructuralEqualsStrategy</c>), by <c>$eq.equals</c> or by the comparison a tuple's or an
    /// anonymous type's members are compared by, and <c>ToString</c> writes what the call writes
    /// (<c>StringConversion.TextGroup</c>): a record's text with each member's number kind, so a
    /// <c>Curve</c>'s points print as singles, a tuple as <c>(1, 2)</c>, an array as <c>System.Int32[]</c> and
    /// an enum by its member's name. Bound to the value itself, a group named a member the value has not, or
    /// one that writes JavaScript's text (#762, #913). The receiver is the helper's ARGUMENT, so it is read
    /// once, when the delegate is made, as C# copies it into the delegate: a receiver that is a call runs
    /// once, and a local reassigned afterwards leaves the delegate holding the value it was made with. A
    /// reference, an anonymous object or an array, is refused there when it is null
    /// (<see cref="Eq.Receiver"/>), as .NET refuses to make the delegate of a null one's method, where a
    /// tuple and a data value, values, are never null. No function is written here as text
    /// (<c>IntroducedFunctionsCoverageTests</c>). <c>GetHashCode</c> never reaches here: its group is
    /// <c>GetHashCodeStrategy</c>'s. A <c>Nullable</c> one's <c>Equals</c> group is its value's, an enum's
    /// included, refused where it is made when the <c>Nullable</c> is empty, which boxes to null, as .NET
    /// refuses it; its <c>ToString</c> group is <c>Nullable</c>'s, which this does not write. Null for any
    /// other member, and for a receiver of another type.
    /// </summary>
    private static JsExpr? RecordMemberGroup(IMethodSymbol method, MemberAccessExpressionSyntax group,
        JsExpr receiver, ConversionContext context)
    {
        if (method.IsStatic || context.SemanticHelper.GetType(group.Expression) is not { } type) return null;
        if (method is { Name: "ToString", Parameters.Length: 0 })
            return StringConversion.TextGroup(type, receiver, context);
        if (method is not { Name: "Equals", Parameters.Length: 1 } || TwinMethodName.HoldsANameOfItsOwn(method))
            return null;
        if (type.UnwrapNullable() is INamedTypeSymbol data && (data.TwinIsData() || data.IsTupleType || data.IsAnonymousType))
        {
            // A Nullable tuple's or data value's group is its value's, and an empty one is refused where the
            // delegate is made: bound to the value, the group named an `equals` no array has, and threw there.
            context.UsedHelpers.Add(Eq.Import);
            var held = data.IsAnonymousType || type.IsNullableValue()
                ? JsExpr.Call(JsExpr.Identifier(Eq.Receiver), receiver)
                : receiver;
            return data.TwinIsData() || ElementEquality.OwnEquals(data) is not { } comparison
                ? JsExpr.Call(JsExpr.Identifier(Eq.EqualsGroup), held)
                : JsExpr.Call(JsExpr.Identifier(Eq.EqualsGroup), held, JsExpr.Opaque(comparison));
        }
        if (type.UnwrapNullable() is INamedTypeSymbol { TypeKind: TypeKind.Enum })
        {
            // A Nullable enum's group is Nullable's Equals, its value's, and an empty one boxes to null,
            // which .NET refuses to make the delegate of, as GetHashCode's group refuses it. Bound to the
            // value, the group named an `equals` no string has, and threw where it was made.
            context.UsedHelpers.Add(Eq.Import);
            return JsExpr.Call(JsExpr.Identifier(Eq.EqualsGroup),
                type.IsNullableValue() ? JsExpr.Call(JsExpr.Identifier(Eq.Receiver), receiver) : receiver);
        }
        // The group of object's Equals(object), or of IEquatable<T>'s, on a receiver whose type says nothing of
        // the value: an interface's own Equals is that member's group, as its call is (Copilot's second review
        // of #947).
        if ((type.SpecialType == SpecialType.System_Object || type.TypeKind is TypeKind.Interface or TypeKind.TypeParameter)
            && (method.ContainingType.SpecialType == SpecialType.System_Object || ElementEquality.IsEquatableEquals(method)))
        {
            context.UsedHelpers.Add(Eq.Import);
            var checkedReceiver = JsExpr.Call(JsExpr.Identifier(Eq.Receiver), receiver);
            var comparator = ElementEquality.IsEquatableEquals(method)
                ? Eq.SameEquatable
                : type is INamedTypeSymbol { TypeKind: TypeKind.Interface } face && ElementEquality.Of(face) == "'item'"
                    ? Eq.SameItem
                    : Eq.SameKey;
            return JsExpr.Call(JsExpr.Identifier(Eq.EqualsGroup), checkedReceiver, JsExpr.Identifier(comparator));
        }
        if (!type.IsNullableValue() && method.ContainingType.SpecialType == SpecialType.System_Object)
        {
            context.UsedHelpers.Add(Eq.Import);
            var checkedReceiver = JsExpr.Call(JsExpr.Identifier(Eq.Receiver), receiver);
            return JsExpr.Call(JsExpr.Identifier(Eq.EqualsGroup), checkedReceiver, JsExpr.Identifier(Eq.SameItem));
        }
        return null;
    }

    public int Priority => 0; // Low priority (fallback)
}
