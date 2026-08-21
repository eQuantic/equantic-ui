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

        // Every form evaluates each operand exactly once: numbers subtract, everything ordered
        // (strings ordinally, chars, Guids-as-strings, longs-as-BigInts) goes through a two-param
        // arrow — the inline three-way used to read both operands twice. Booleans order
        // false < true. (Number CompareTo keeps its known NaN caveat: C# orders NaN below
        // everything; a subtraction answers NaN.)
        var kind = ReceiverKind(invocation, context);
        var template = kind switch
        {
            SpecialType.System_Boolean => "(($a, $b) => $a === $b ? 0 : $a ? 1 : -1)({0}, {1})",
            SpecialType.System_Int32 => "Math.sign({0} - {1})",
            // char.CompareTo is the raw code-unit SUBTRACTION — 'z'.CompareTo('a') is 25, not 1.
            SpecialType.System_Char => "(($a, $b) => $a.charCodeAt(0) - $b.charCodeAt(0))({0}, {1})",
            _ => "(($a, $b) => $a < $b ? -1 : $a > $b ? 1 : 0)({0}, {1})",
        };
        return TemplateFill.With(template, left, right);
    }

    /// <summary>The receiver's comparison FAMILY (subtraction, ordered, boolean), or null when it
    /// is a type that carries its own comparison (a record the compiler emits, an enum) and must
    /// keep the call.</summary>
    private static SpecialType? ReceiverKind(InvocationExpressionSyntax invocation, ConversionContext context)
    {
        var access = (MemberAccessExpressionSyntax)invocation.Expression;
        var type = context.SemanticHelper.GetType(access.Expression);
        // Guid is deliberately ABSENT: Guid.CompareTo orders by the struct's COMPONENTS, which is
        // not the string order a guid rides as here — a sort that disagrees across the seam is
        // worse than a fence.
        return type?.SpecialType switch
        {
            // Decimal is deliberately ABSENT: it rides as a runtime Decimal object, and the old
            // subtraction claim answered Math.sign(object - object) = NaN, silently. Fenced until
            // the Decimal twin grows a comparison.
            SpecialType.System_Int32 or SpecialType.System_Double
                or SpecialType.System_Single or SpecialType.System_Int16
                or SpecialType.System_Byte => SpecialType.System_Int32,
            // A long is a BigInt: subtraction answers a BigInt, so it takes the ordered arrow.
            SpecialType.System_Int64 => SpecialType.System_String,
            SpecialType.System_String => SpecialType.System_String,
            SpecialType.System_Char => SpecialType.System_Char,
            SpecialType.System_Boolean => SpecialType.System_Boolean,
            _ => null,
        };
    }

    /// <summary>Above the general invocation fallback, which would emit the call verbatim.</summary>
    public int Priority => 30;
}
