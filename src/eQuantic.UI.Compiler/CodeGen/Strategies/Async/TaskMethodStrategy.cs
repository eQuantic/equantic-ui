using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Async;

/// <summary>
/// Strategy for Task static methods.
/// Handles:
/// - Task.Delay(ms) -> Promise delay
/// - Task.Run(fn) -> Promise wrapper
/// - Task.WhenAll(tasks) -> Promise.all
/// </summary>
public class TaskMethodStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;

        // Heuristic check for "Task"
        var expr = memberAccess.Expression.ToString();
        var name = memberAccess.Name.Identifier.Text;
        
        if (expr != "Task" && expr != "System.Threading.Tasks.Task") return false;
        
        return name is "Delay" or "Run" or "WhenAll" or "WhenAny" or "FromResult";
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var name = memberAccess.Name.Identifier.Text;
        var args = invocation.ArgumentList.Arguments;
        
        if (name == "Delay")
        {
            var ms = context.Converter.ConvertExpression(args[0].Expression);
            return $"new Promise(resolve => setTimeout(resolve, {ms}))";
        }
        
        if (name == "Run")
        {
            var fn = context.Converter.ConvertExpression(args[0].Expression);
            // Task.Run(() => ...) -> Promise.resolve().then(() => ...)
            // Or just execute immediately inside promise to offload? JS is single threaded event loop.
            // Usually Promise.resolve().then(fn) works best to schedule microtask.
            return $"Promise.resolve().then({fn})";
        }
        
        if (name == "WhenAll")
        {
            var arg = context.Converter.ConvertExpression(args[0].Expression);
            // If variadic args? Task.WhenAll(t1, t2) -> Promise.all([t1, t2])
            if (args.Count > 1) 
            {
                 var allArgs = string.Join(", ", args.Select(a => context.Converter.ConvertExpression(a.Expression)));
                 return $"Promise.all([{allArgs}])";
            }
            return $"Promise.all({arg})";
        }
        
        if (name == "WhenAny")
        {
            var arg = context.Converter.ConvertExpression(args[0].Expression);
            if (args.Count > 1) 
            {
                 var allArgs = string.Join(", ", args.Select(a => context.Converter.ConvertExpression(a.Expression)));
                 return $"Promise.race([{allArgs}])";
            }
            return $"Promise.race({arg})";
        }
        
        if (name == "FromResult")
        {
            var val = context.Converter.ConvertExpression(args[0].Expression);
            return $"Promise.resolve({val})";
        }

        return node.ToString();
    }

    public int Priority => 10;
}
