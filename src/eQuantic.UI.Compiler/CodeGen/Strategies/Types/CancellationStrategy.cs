using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// System.Threading's cancellation trio as the runtime's (<c>utils/cancellation.ts</c>), which keeps
/// .NET's behaviour, measured: <c>CancellationTokenSource</c>, <c>CancellationToken</c> and the
/// <c>CancellationTokenRegistration</c> a callback is given back as. What C# BUILDS is built through
/// <c>$eq.cancellation</c>: <c>new CancellationTokenSource(delay?)</c>, <c>CancellationToken.None</c>
/// (and <c>default</c>, see <see cref="DefaultValue"/>), <c>new CancellationToken(canceled)</c> and
/// <c>CreateLinkedTokenSource</c>. A member the runtime's twin carries is that twin's member in
/// camelCase, and any other member of the three is refused (EQ2004), where it went out under its own
/// name and failed only when the browser called it.
/// <para>
/// The code engine's completion was the first write-once code to ask a provider for an answer it may
/// stop wanting (#296): a source per request, cancelled by the next one. Nothing in the translation
/// knew the trio, so <c>new CancellationTokenSource()</c> named a class no module defined.
/// </para>
/// </summary>
public class CancellationStrategy : IExpressionIrStrategy
{
    private const string Source = "System.Threading.CancellationTokenSource";
    private const string Token = "System.Threading.CancellationToken";
    private const string Registration = "System.Threading.CancellationTokenRegistration";

    /// <summary>The members each runtime twin carries, by the type that declares them. Equality and the
    /// hash code are every value's, and <c>==</c> is the twin's identity: a source hands out one token,
    /// and the token that never cancels, like the one that always has, is one value.</summary>
    private static readonly Dictionary<string, HashSet<string>> Members = new()
    {
        [Source] = ["Token", "IsCancellationRequested", "Cancel", "CancelAfter", "Dispose"],
        [Token] = ["IsCancellationRequested", "CanBeCanceled", "ThrowIfCancellationRequested", "Register", "Equals", "GetHashCode"],
        [Registration] = ["Token", "Unregister", "Dispose"],
    };

    public bool CanConvert(SyntaxNode node, ConversionContext context) => node switch
    {
        BaseObjectCreationExpressionSyntax creation => context.SemanticHelper.GetType(creation) is { } type
            && (type.IsNamed(Source) || type.IsNamed(Token)),
        InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax method } => DeclaredByTrio(method, context),
        // The callee of a call is the call's to translate, with its arguments.
        MemberAccessExpressionSyntax access => access.Parent is not InvocationExpressionSyntax { Expression: var callee }
            || callee != access
                ? DeclaredByTrio(access, context)
                : false,
        _ => false,
    };

    /// <summary>Whether the member reached is one of the trio's, by its symbol: the model always knows a
    /// BCL member, so the spelling is never consulted.</summary>
    private static bool DeclaredByTrio(MemberAccessExpressionSyntax access, ConversionContext context) =>
        context.SemanticHelper.GetSymbol(access) is { ContainingType: { } declaring }
        && Members.ContainsKey(declaring.OriginalDefinition.ToDisplayString());

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        switch (node)
        {
            case BaseObjectCreationExpressionSyntax creation:
                return Creation(creation, context);

            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax method } invocation:
            {
                var symbol = (IMethodSymbol)context.SemanticHelper.GetSymbol(method)!;
                var arguments = invocation.ArgumentList.Arguments
                    .Select(argument => context.Converter.ConvertIr(argument.Expression)).ToList();
                if (symbol is { IsStatic: true, Name: "CreateLinkedTokenSource" })
                    return JsExpr.Call(JsExpr.Identifier(Eq.CancellationLinked), arguments);
                if (!Supported(symbol))
                    return Refused(invocation, symbol, context);
                return JsExpr.Call(
                    JsExpr.Member(context.Converter.ConvertIr(method.Expression), symbol.Name.ToCamelCase()),
                    arguments);
            }

            case MemberAccessExpressionSyntax access:
            {
                var symbol = context.SemanticHelper.GetSymbol(access)!;
                if (symbol is { IsStatic: true, Name: "None" }) return JsExpr.Identifier(Eq.CancellationNone);
                if (!Supported(symbol)) return Refused(access, symbol, context);
                var receiver = context.Converter.ConvertIr(access.Expression);
                if (symbol is not IMethodSymbol) return JsExpr.Member(receiver, symbol.Name.ToCamelCase());
                // A method REFERENCE is a method group, and a group keeps its receiver, read once, as
                // C# reads it when the delegate is made: `register(inner.cancel)` lost it, and the
                // callback threw a TypeError on `this` when the token cancelled.
                return JsExpr.Template($"{{0}}.{symbol.Name.ToCamelCase()}.bind({{0}})", [receiver],
                    context.TypeAnnotations);
            }
        }
        return JsExpr.Opaque(context.Unhandled(node, "the cancellation trio"));
    }

    /// <summary><c>new CancellationTokenSource()</c>, with a delay after which it cancels, and
    /// <c>new CancellationToken(canceled)</c>: the one cancelled token, or the one that never is.</summary>
    private static JsExpr Creation(BaseObjectCreationExpressionSyntax creation, ConversionContext context)
    {
        var arguments = (creation.ArgumentList?.Arguments ?? default)
            .Select(argument => context.Converter.ConvertIr(argument.Expression)).ToList();
        if (context.SemanticHelper.GetType(creation).IsNamed(Token))
            return arguments.Count == 0
                ? JsExpr.Identifier(Eq.CancellationNone)
                : JsExpr.Call(JsExpr.Identifier(Eq.CancellationToken), arguments);
        return JsExpr.Call(JsExpr.Identifier(Eq.CancellationSource), arguments);
    }

    /// <summary>Whether the runtime's twin carries <paramref name="member"/>: one of the names above,
    /// and of <c>Register</c> only the overload that takes a callback and nothing else.</summary>
    private static bool Supported(ISymbol member) =>
        Members.TryGetValue(member.ContainingType.OriginalDefinition.ToDisplayString(), out var names)
        && names.Contains(member.Name)
        && member is not IMethodSymbol { Name: "Register", Parameters.Length: not 1 }
        && member is not IMethodSymbol { Name: "Register", Parameters: [{ Type.TypeKind: not TypeKind.Delegate }] }
        && member is not IMethodSymbol { Name: "Register", Parameters: [{ Type: INamedTypeSymbol { DelegateInvokeMethod.Parameters.Length: > 0 } }] }
        && member is not IMethodSymbol { Name: "Cancel", Parameters.Length: > 0 };

    private static JsExpr Refused(SyntaxNode node, ISymbol member, ConversionContext context)
    {
        context.Report(node, ConversionSeverity.Error, "EQ2004",
            $"'{member.ContainingType.ToDisplayString()}.{member.Name}' has no JavaScript translation: the "
            + "runtime's cancellation carries the members a cancellation is made of (a source's Token, "
            + "Cancel, CancelAfter and Dispose, a token's IsCancellationRequested, CanBeCanceled, "
            + "ThrowIfCancellationRequested and Register of a callback), and this is not one of them.");
        return JsExpr.Opaque("undefined");
    }

    public int Priority => 15;
}
