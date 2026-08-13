using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// <c>form.Changed += handler</c> and <c>-=</c> — subscribing to an EVENT, which JavaScript has no
/// operator for.
/// <para>
/// Emitted literally, `+=` on a null field is string concatenation: `null + function` produced the
/// text of the lambda, and the next raise threw "changed is not a function" from inside a component
/// — on the client only, because the server was running the same C# correctly. A page that
/// subscribed to its own form's changes silently stopped re-rendering after hydration.
/// </para>
/// <para>
/// The rewrite composes an invocation list through <c>$eq.delegates</c>, which keeps what C#
/// promises: handlers run in the order they were added, and removing one drops the LAST occurrence.
/// Gated on the left side being an EVENT — a numeric `+=` is still addition, and a plain
/// <c>Action</c> FIELD keeps its own meaning too, because assigning over one is what apps expect
/// there.
/// </para>
/// </summary>
public class EventSubscriptionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not AssignmentExpressionSyntax assignment) return false;
        if (!assignment.IsKind(SyntaxKind.AddAssignmentExpression)
            && !assignment.IsKind(SyntaxKind.SubtractAssignmentExpression)) return false;

        // Only an event. Without the model this cannot be decided, and guessing from the shape
        // would turn `total += price` into a delegate composition.
        return context.SemanticHelper.GetSymbol(assignment.Left) is IEventSymbol;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var assignment = (AssignmentExpressionSyntax)node;
        var target = context.Converter.ConvertExpression(assignment.Left);
        var handler = context.Converter.ConvertExpression(assignment.Right);
        var helper = assignment.IsKind(SyntaxKind.AddAssignmentExpression)
            ? Eq.CombineDelegate
            : Eq.RemoveDelegate;

        context.UsedHelpers.Add(Eq.Import);
        return $"{target} = {helper}({target}, {handler})";
    }

    /// <summary>Above the generic assignment path, which would emit the `+=` verbatim.</summary>
    public int Priority => 14;
}
