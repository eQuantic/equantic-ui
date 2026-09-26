using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// `governing switch { pattern [when …] => result, … }` → an IIFE that binds the value once and returns
/// the first matching arm's result. Pattern conditions and the variables each arm binds are produced by
/// the shared <see cref="PatternConverter"/> (so switch expressions, switch statements and `is` patterns
/// stay consistent); a bound arm declares its variables before the when-clause and result.
/// </summary>
public class SwitchExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is SwitchExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var switchExpr = (SwitchExpressionSyntax)node;
        var governingExpr = context.Converter.ConvertExpression(switchExpr.GoverningExpression);
        var governingType = context.SemanticHelper.GetType(switchExpr.GoverningExpression);

        var sb = new StringBuilder();
        sb.Append("(() => {");
        // Variables declared inside an ARM'S OWN expression or guard (`… => Maybe(v) is { } bound ? …`,
        // `… when int.TryParse(s, out var n) => n`) are ASSIGNED by the converted code and were never
        // declared, so the arm threw ReferenceError the moment it matched — in a module, which is
        // strict mode. The IIFE this strategy already emits is the scope they belong to, declared
        // ONCE: every arm is its own C# scope, so two arms may bind the same name, and declaring it
        // per arm was two `let`s of one name in one function.
        sb.Append(ExpressionVariableScanner.Declarations(switchExpr.Arms
            .SelectMany(arm => ExpressionVariableScanner.Names(arm.Expression)
                .Concat(ExpressionVariableScanner.Names(arm.WhenClause?.Condition))),
            context.TypeAnnotations));
        sb.Append($" const _s = {governingExpr};");

        foreach (var arm in switchExpr.Arms)
        {
            if (arm.Pattern is DiscardPatternSyntax && arm.WhenClause == null)
            {
                sb.Append($" return {context.Converter.ConvertExpression(arm.Expression)};");
                break; // discard matches everything
            }

            var condition = PatternConverter.BuildCondition(arm.Pattern, "_s", context, governingType);
            var bindings = new List<(string Name, string Access)>();
            PatternConverter.CollectBindings(arm.Pattern, "_s", context, bindings, governingType);
            var armResult = context.Converter.ConvertExpression(arm.Expression);

            if (bindings.Count > 0)
            {
                var declare = string.Concat(bindings.Select(b => $"const {b.Name} = {b.Access}; "));
                var inner = $"return {armResult};";
                if (arm.WhenClause != null)
                    inner = $"if ({context.Converter.ConvertExpression(arm.WhenClause.Condition)}) return {armResult};";
                sb.Append($" if ({condition}) {{ {declare}{inner} }}");
            }
            else
            {
                if (arm.WhenClause != null)
                    condition = $"({condition}) && ({context.Converter.ConvertExpression(arm.WhenClause.Condition)})";
                sb.Append($" if ({condition}) return {armResult};");
            }
        }

        if (!switchExpr.Arms.Any(a => a.Pattern is DiscardPatternSyntax && a.WhenClause == null))
            sb.Append(" return null;");

        sb.Append(" })()");
        return sb.ToString();
    }

    public int Priority => 0;
}
