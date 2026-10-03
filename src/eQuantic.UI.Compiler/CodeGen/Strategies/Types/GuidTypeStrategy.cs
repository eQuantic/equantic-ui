using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// A Guid's statics and its string constructor: <c>Guid.Empty</c>, <c>NewGuid()</c>, <c>Parse</c>,
/// <c>TryParse</c> and <c>new Guid(text)</c>. A Guid is its text in the browser, so each that makes one
/// from text answers the canonical text, the lowercase <c>D</c> format, through the runtime
/// (<c>utils/guid.ts</c>): <c>Parse</c> handed its argument back as written, so two spellings of one
/// Guid were two values to <c>==</c>, a dictionary and a set, and <c>new Guid(text)</c> named a class
/// nothing defines (#459). Decided by the bound symbol, and by the spelling only where no model can be
/// asked.
/// </summary>
public class GuidTypeStrategy : IExpressionIrStrategy
{
    private const string Empty = "'00000000-0000-0000-0000-000000000000'";

    public bool CanConvert(SyntaxNode node, ConversionContext context) => node switch
    {
        MemberAccessExpressionSyntax access => IsGuid(access.Expression, context),
        InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax method } => IsGuid(method.Expression, context),
        BaseObjectCreationExpressionSyntax creation => context.SemanticHelper.GetType(creation).IsNamed("System.Guid")
            && context.SemanticHelper.GetOperation(creation) is IObjectCreationOperation
            {
                Constructor.Parameters: [{ Type.SpecialType: SpecialType.System_String }],
            },
        _ => false,
    };

    /// <summary>The type <c>System.Guid</c> itself, as the receiver of a static, by its symbol, or by
    /// its spelling where no model can be asked.</summary>
    private static bool IsGuid(ExpressionSyntax receiver, ConversionContext context) =>
        context.SemanticHelper.GetSymbol(receiver) is ITypeSymbol type
            ? type.ToDisplayString() == "System.Guid"
            : receiver is IdentifierNameSyntax { Identifier.Text: "Guid" } && context.CanGuess(receiver);

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        if (node is BaseObjectCreationExpressionSyntax creation)
            return Parsed(creation.ArgumentList!.Arguments[0].Expression, context);

        if (node is MemberAccessExpressionSyntax { Name.Identifier.Text: "Empty" })
            return JsExpr.Literal(Empty);

        if (node is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax method } invocation)
        {
            var arguments = invocation.ArgumentList.Arguments;
            switch (method.Name.Identifier.Text)
            {
                case "NewGuid":
                    return JsExpr.Callish("crypto.randomUUID()");
                case "Parse" when arguments.Count == 1:
                    return Parsed(arguments[0].Expression, context);
                case "TryParse" when arguments.Count == 2
                    && arguments.FirstOrDefault(argument => !argument.RefKindKeyword.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.OutKeyword))
                        is { } input
                    && arguments.FirstOrDefault(argument => argument.RefKindKeyword.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.OutKeyword))
                        is { } result:
                    context.UsedHelpers.Add(Eq.Import);
                    // A failed TryParse leaves Guid.Empty in its out argument, as .NET leaves it.
                    return JsExpr.Callish(OutArgument.TryAnswer(result, input.SpanStart,
                        JsExpr.Call(JsExpr.Identifier(Eq.GuidTryParse), context.Converter.ConvertIr(input.Expression)),
                        Empty, context));
            }
        }

        return JsExpr.Opaque(context.Unhandled(node, "Guid"));
    }

    /// <summary>The canonical text of a Guid read from <paramref name="text"/>, or .NET's refusal.</summary>
    private static JsExpr Parsed(ExpressionSyntax text, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        return JsExpr.Call(JsExpr.Identifier(Eq.GuidParse), context.Converter.ConvertIr(text));
    }

    public int Priority => 10;
}
