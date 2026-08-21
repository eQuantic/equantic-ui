using eQuantic.UI.Compiler.CodeGen.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// The fallback for <c>receiver.Member</c> once every dedicated strategy has declined: a handful of
/// well-known statics (<c>DateTime.Now</c>, <c>Guid.Empty</c>), the type-dependent <c>Count</c>
/// (<c>size</c> on a Set, <c>Object.keys().length</c> on a dictionary, <c>length</c> on a sequence),
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

        // .Count is type-dependent: Set -> .size, Dictionary -> Object.keys(x).length,
        // List/array/ICollection -> .length.
        if (name == "Count")
        {
            var def = context.SemanticHelper.GetType(memberAccess.Expression)?.OriginalDefinition?.ToString() ?? "";
            if (def.StartsWith("System.Collections.Generic.HashSet") ||
                def.StartsWith("System.Collections.Generic.ISet") ||
                def.StartsWith("System.Collections.Generic.IReadOnlySet"))
                return JsExpr.Member(receiver, "size");
            if (def.StartsWith("System.Collections.Generic.Dictionary") ||
                def.StartsWith("System.Collections.Generic.IDictionary") ||
                def.StartsWith("System.Collections.Generic.IReadOnlyDictionary"))
                return JsExpr.Member(JsExpr.Call(JsExpr.Identifier("Object.keys"), receiver), "length");

            // Same coin toss as `Contains`: a receiver typed only as a collection may be a Set at
            // run time, whose count is `size`. `.length` on one is undefined — and `undefined > 0`
            // is false, so the header checkbox simply never noticed a selection.
            if (context.SemanticHelper.GetType(memberAccess.Expression).HasOpenCollectionShape())
            {
                context.UsedHelpers.Add(Eq.Import);
                return JsExpr.Call(JsExpr.Identifier(Eq.Count), receiver);
            }

            // A USER type with its own `Count` is not a collection — its property emits as
            // `get count()`, and rewriting the read to `.length` returned undefined. `.length` stays
            // the answer for every real sequence, and for an unresolved receiver (untyped code,
            // where guessing array is the useful default).
            var receiverType = context.SemanticHelper.GetType(memberAccess.Expression);
            if (receiverType is not null && receiverType.Locations.Any(location => location.IsInSource))
                return JsExpr.Member(receiver, name.ToCamelCase());

            return JsExpr.Member(receiver, "length");
        }

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
        if (symbol is IMethodSymbol)
        {
            var isDirectInvocation = memberAccess.Parent is InvocationExpressionSyntax invocation &&
                                  invocation.Expression == memberAccess;
            if (!isDirectInvocation)
                return JsExpr.Call(JsExpr.Member(member, "bind"), receiver);
        }

        return member;
    }

    public int Priority => 0; // Low priority (fallback)
}
