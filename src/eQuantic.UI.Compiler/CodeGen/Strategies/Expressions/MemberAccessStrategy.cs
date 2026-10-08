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
        var name = memberAccess.Name.Identifier.Text;
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
        if (symbol is not null)
            symbol.ReportIfHostOnly(node, context, context.SemanticHelper.GetType(memberAccess.Expression));

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

        // .Count is type-dependent, and one table answers it for a member access and a pattern alike.
        if (name == "Count")
            return CountSpelling.Read(receiver, context.SemanticHelper.GetType(memberAccess.Expression), context);

        // The camelCase guess below is exactly the invocation fallback's story (EQ2006): an
        // in-tree access an AUTHORITATIVE model could not bind is missing references or code that
        // doesn't compile — before this, an unresolved PascalCase access could even fall into the
        // enum shape-heuristic and ship as a member-name string. Guessing stays legal where it is
        // honest: snippets, rewritten nodes, non-authoritative hosts.
        if (symbol is null && !context.CanGuess(node))
        {
            context.Report(node, ConversionSeverity.Error, "EQ2006",
                $"'{memberAccess.Name.Identifier.Text}' does not bind in the compiler's semantic model, "
                + "so any translation would be a guess. Either this code does not compile, or the "
                + "compiler is missing references/generated sources — the SDK passes them via "
                + "--refs/--generated; a custom host must do the same.");
        }

        name = name switch
        {
            "Length" => "length",
            "Count" => "length", // Arrays/Lists (fallback when type is unknown)
            _ => name.ToCamelCase()
        };

        if (string.IsNullOrEmpty(name)) return receiver;

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
    /// <c>Color</c> and <c>Curve</c>): the delegate its call is. <c>Equals</c> answers through
    /// <c>$eq.equals</c>, as <c>StructuralEqualsStrategy</c> lowers <c>a.Equals(b)</c>, and
    /// <c>ToString</c> through the record text <c>ToStringStrategy</c> writes, each member's number
    /// kind included, so a <c>Curve</c>'s points print as singles. The receiver is the argument of the
    /// function that makes the delegate, so it is read once, when the delegate is made, as C# copies it
    /// into the delegate: a receiver that is a call runs once, and a local reassigned afterwards leaves
    /// the delegate holding the value it was made with. <c>GetHashCode</c> never reaches here: its group
    /// is <c>GetHashCodeStrategy</c>'s. Null for any other member, and for a receiver of another type,
    /// a <c>Nullable</c> of a data twin among them, whose members are <c>Nullable</c>'s.
    /// </summary>
    private static JsExpr? RecordMemberGroup(IMethodSymbol method, MemberAccessExpressionSyntax group,
        JsExpr receiver, ConversionContext context)
    {
        if (method.IsStatic
            || context.SemanticHelper.GetType(group.Expression) is not INamedTypeSymbol data
            || !data.TwinIsData())
            return null;

        // `$value` and `$other`: no C# name can take either, so nothing the receiver names is shadowed.
        if (method is { Name: "Equals", Parameters.Length: 1 })
        {
            context.UsedHelpers.Add(Eq.Import);
            var other = context.TypeAnnotations ? "$other: unknown" : "$other";
            return JsExpr.Template($"(($value) => ({other}) => {Eq.Equals}($value, $other))({{0}})", receiver);
        }
        if (method is { Name: "ToString", Parameters.Length: 0 })
        {
            var text = StringConversion.ToDotNetString(group.Expression, JsExpr.Identifier("$value"), context);
            return JsExpr.Template($"(($value) => () => {JsExprWriter.Write(text)})({{0}})", receiver);
        }
        return null;
    }

    public int Priority => 0; // Low priority (fallback)
}
