using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// The three LINQ sequences that come from NOTHING rather than from another sequence —
/// <c>Enumerable.Range</c>, <c>Repeat</c> and <c>Empty</c>. Every other LINQ operator had a strategy
/// because it hangs off a receiver; these are statics, so they fell through to the fallback and were
/// emitted as <c>Enumerable.range(…)</c> — a name the browser has never heard of.
/// <para>
/// A sequence IS a JS array here (that is what every other LINQ strategy assumes). Range and Repeat
/// are the runtime's (<c>$eq.linq.range</c>, <c>$eq.linq.repeat</c>), their arguments evaluated once
/// at the call, as C# evaluates them, and refused as .NET refuses them. They were <c>Array.from</c>
/// with a callback around the argument's C#: <c>Range(Start(), 3)</c> called <c>Start</c> three times,
/// <c>Repeat(new List&lt;int&gt;(), 3)</c> made three lists where .NET repeats one, and an <c>await</c>
/// in either argument landed in the callback, which is not async (#539).
/// </para>
/// </summary>
public class EnumerableFactoryStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;

        var receiver = memberAccess.Expression.ToString();
        if (receiver is not ("Enumerable" or "System.Linq.Enumerable")) return false;

        return Name(memberAccess) is "Range" or "Repeat" or "Empty";
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var arguments = invocation.ArgumentList.Arguments;

        var helper = Name(memberAccess) switch
        {
            "Range" when arguments.Count == 2 => Eq.LinqRange,
            "Repeat" when arguments.Count == 2 => Eq.LinqRepeat,
            _ => null,
        };
        if (helper is null) return JsExpr.Array([]);
        context.UsedHelpers.Add(Eq.Import);

        // The parts in the order C# evaluates them, the order they are written, each in the hole of the
        // parameter it binds to: `Range(count: 3, start: F())` passes F() first. The template writer
        // binds the parts when the two orders differ.
        var parts = arguments.Select(argument => context.Converter.ConvertIr(argument.Expression)).ToList();
        var method = context.SemanticHelper.GetSymbol(invocation) as IMethodSymbol;
        var holes = new int[] { 0, 1 };
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].NameColon?.Name.Identifier.ValueText is { } name
                && method?.Parameters.FirstOrDefault(parameter => parameter.Name == name) is { } parameter)
                holes[parameter.Ordinal] = i;
        }
        return JsExpr.Template($"{helper}({{{holes[0]}}}, {{{holes[1]}}})", parts, context.TypeAnnotations);
    }

    /// <summary>The member name, with any type argument (<c>Empty&lt;string&gt;</c>) set aside.</summary>
    private static string Name(MemberAccessExpressionSyntax memberAccess) =>
        memberAccess.Name.Identifier.Text;

    public int Priority => 12;
}
