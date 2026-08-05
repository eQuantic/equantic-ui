using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

/// <summary>
/// `a.CompareTo(b)` on a number or a string — the ordering idiom every comparable type is written
/// with. JavaScript numbers and strings have no such method, so the call reached the browser
/// verbatim: `Property 'compareTo' does not exist on type 'number'`, and every sort or ordering
/// comparison built on it was dead.
/// <para>
/// The contract is only the SIGN, which is what every call site reads (`&lt; 0`, `&gt; 0`, `== 0`),
/// so a subtraction is the faithful translation for numbers and a two-way comparison for strings.
/// A user type keeps its own `compareTo` — the emitter gives it one.
/// </para>
/// </summary>
public class CompareToStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context) =>
        node is InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "CompareTo" },
            ArgumentList.Arguments.Count: 1,
        } invocation
        && ReceiverKind(invocation, context) is not null;

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var access = (MemberAccessExpressionSyntax)invocation.Expression;
        var left = context.Converter.ConvertExpression(access.Expression);
        var right = context.Converter.ConvertExpression(invocation.ArgumentList.Arguments[0].Expression);

        return ReceiverKind(invocation, context) == SpecialType.System_String
            ? $"({left} < {right} ? -1 : {left} > {right} ? 1 : 0)"
            : $"Math.sign({left} - {right})";
    }

    /// <summary>The receiver's primitive kind, or null when it is a type that carries its own
    /// comparison (a record the compiler emits, an enum) and must keep the call.</summary>
    private static SpecialType? ReceiverKind(InvocationExpressionSyntax invocation, ConversionContext context)
    {
        var access = (MemberAccessExpressionSyntax)invocation.Expression;
        var type = context.SemanticHelper.GetType(access.Expression);
        return type?.SpecialType switch
        {
            SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_Double
                or SpecialType.System_Single or SpecialType.System_Decimal or SpecialType.System_Int16
                or SpecialType.System_Byte => SpecialType.System_Int32,
            SpecialType.System_String => SpecialType.System_String,
            _ => null,
        };
    }

    /// <summary>Above the general invocation fallback, which would emit the call verbatim.</summary>
    public int Priority => 30;
}
