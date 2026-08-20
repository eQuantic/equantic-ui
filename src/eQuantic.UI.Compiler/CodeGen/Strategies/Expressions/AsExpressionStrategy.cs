using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// C# <c>x as T</c>: the value when it IS a <c>T</c>, <c>null</c> when it is not. Emitted through
/// the SAME type test patterns use (<see cref="PatternConverter.TypeCheck"/>), so <c>x as T</c> and
/// <c>x is T</c> can never disagree about what counts as a <c>T</c>: <c>typeof</c> for primitives,
/// <c>instanceof</c> for vocabulary classes, and the null-check patterns fall back to for
/// everything else. This used to be a passthrough — <c>x as T</c> emitted plain <c>x</c> — which
/// made <c>if (x as Foo != null)</c> take the branch for ANY non-null <c>x</c>.
/// </summary>
public class AsExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is BinaryExpressionSyntax binary && binary.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AsExpression);
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var binary = (BinaryExpressionSyntax)node;
        var value = context.Converter.ConvertExpression(binary.Left);

        // `x as T?` tests against T: the C# result type is the nullable, the runtime test is not.
        var type = binary.Right as TypeSyntax;
        if (type is NullableTypeSyntax nullable) type = nullable.ElementType;
        if (type is null)
            return context.Unhandled(node, "as-operator");

        // An arrow with a parameter evaluates `x` once — `(typeof x === 'string' ? x : null)`
        // would evaluate a side-effecting receiver twice. `: any` matches how other synthesized
        // bindings are annotated when the output is TypeScript.
        var parameter = context.TypeAnnotations ? "($as: any)" : "$as";
        var test = PatternConverter.TypeCheck(type, "$as", context);
        return $"({parameter} => {test} ? $as : null)({value})";
    }

    public int Priority => 10;
}
