using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;

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
/// Gated on the left side being an EVENT or a DELEGATE, an <c>Action</c> field, local or property included,
/// whose <c>+=</c> is <c>Delegate.Combine</c> just the same: a numeric `+=` is still addition. A delegate
/// field's was emitted literally, so the backing field an event's own accessors compose into held the text
/// of its handlers, and the event threw when raised (#746).
/// </para>
/// </summary>
public class EventSubscriptionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not AssignmentExpressionSyntax assignment) return false;
        if (!assignment.IsKind(SyntaxKind.AddAssignmentExpression)
            && !assignment.IsKind(SyntaxKind.SubtractAssignmentExpression)) return false;

        // Only an event or a delegate. Without the model this cannot be decided, and guessing from the
        // shape would turn `total += price` into a delegate composition.
        return context.SemanticHelper.GetSymbol(assignment.Left) is IEventSymbol
            || context.SemanticHelper.GetType(assignment.Left) is { TypeKind: TypeKind.Delegate };
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var assignment = (AssignmentExpressionSyntax)node;
        var adds = assignment.IsKind(SyntaxKind.AddAssignmentExpression);
        var helper = adds ? Eq.CombineDelegate : Eq.RemoveDelegate;
        context.UsedHelpers.Add(Eq.Import);
        // A target evaluates once, as C# does: `Get().Run += h` called Get twice.
        if (context.SemanticHelper.GetSymbol(assignment.Left) is not IEventSymbol symbol)
        {
            JsExpr Next(JsExpr current, IReadOnlyList<JsExpr> operands) => JsExpr.Call(JsExpr.Identifier(helper), current, operands[0]);
            var value = context.Converter.ConvertIr(assignment.Right);
            return JsExprWriter.Write(Place.Of(assignment.Left, context) is { } place
                ? place.Modify([value], Next, answerOld: false)
                : ReadModifyWrite.Assign(context.Converter.ConvertIr(assignment.Left), [value], Next, answerOld: false, context));
        }
        var target = context.Converter.ConvertExpression(assignment.Left);
        return Subscription(symbol, assignment, target,
            () => Receiver(assignment.Left, symbol, symbol.MemberTwinName(), target, context), context);
    }

    /// <summary>
    /// A subscription to <paramref name="symbol"/>, <c>+=</c> or <c>-=</c>, read off what <paramref name="receiver"/>
    /// answers, asked only where the event has accessors to call, so a field-like one converts its receiver once:
    /// the event's accessors, as C# compiles the subscription into a call of them (#746), or its invocation list,
    /// composed in <paramref name="target"/>, for a field-like event and where no receiver is known. One lowering
    /// for a plain subscription and a null-conditional one (<c>s?.Fired += h</c>), whose receiver is the value
    /// its guard read once (<see cref="NullConditionalAssignment"/>, #1445). An accessor no twin holds is
    /// refused here, where either is written (EQ2010).
    /// </summary>
    internal static string Subscription(IEventSymbol symbol, AssignmentExpressionSyntax assignment, string target,
        Func<string?> receiver, ConversionContext context)
    {
        var adds = assignment.IsKind(SyntaxKind.AddAssignmentExpression);
        context.UsedHelpers.Add(Eq.Import);
        (adds ? symbol.AddMethod : symbol.RemoveMethod)?.ReportIfAccessorStaysOnTheServer(assignment, context);
        var handler = context.Converter.ConvertExpression(assignment.Right);

        // An event declared with accessors was written nowhere, and a subscription through an interface wrote a
        // key its explicit implementation's accessors never read.
        if (EventAccessors.Of(symbol) is var kind and not EventAccessors.Kind.FieldLike && receiver() is { } read)
        {
            var name = symbol.MemberTwinName();
            return kind == EventAccessors.Kind.Declared
                ? $"{read}.{EventAccessors.Name(adds ? "add" : "remove", name)}({handler})"
                : $"{(adds ? Eq.AddToEvent : Eq.RemoveFromEvent)}({read}, '{name}', {handler})";
        }

        var helper = adds ? Eq.CombineDelegate : Eq.RemoveDelegate;
        return $"{target} = {helper}({target}, {handler})";
    }

    /// <summary>
    /// What the event is read off, from the syntax: the receiver a member access names, <c>this</c> for an
    /// instance event named bare inside its type, and for a static one named bare the type its read was
    /// written through (<paramref name="target"/> less the event's name), or null where none of these holds.
    /// </summary>
    private static string? Receiver(ExpressionSyntax left, IEventSymbol symbol, string name, string target,
        ConversionContext context) => left switch
    {
        MemberAccessExpressionSyntax access => context.Converter.ConvertExpression(access.Expression),
        IdentifierNameSyntax when !symbol.IsStatic => "this",
        IdentifierNameSyntax when target.EndsWith("." + name, StringComparison.Ordinal) => target[..^(name.Length + 1)],
        _ => null,
    };

    /// <summary>Above the generic assignment path, which would emit the `+=` verbatim.</summary>
    public int Priority => 14;
}
