using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

/// <summary>
/// Strategy for static String methods.
/// Handles:
/// - String.IsNullOrEmpty(s) -> !s
/// - String.Join(sep, val) -> val.join(sep)
/// - String.Format(fmt, args) -> fmt.replace... (Simplified)
/// </summary>
public class StringStaticStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        // `string.Empty` — the static PROPERTY (no invocation): the empty string literal.
        if (node is MemberAccessExpressionSyntax { Name.Identifier.Text: "Empty" } property
            && property.Expression.ToString() is "string" or "String" or "System.String")
            return true;

        if (node is not InvocationExpressionSyntax invocation) return false;
        
        var methodAccess = invocation.Expression as MemberAccessExpressionSyntax;
        if (methodAccess == null) return false;

        var typeExpression = methodAccess.Expression.ToString();
        var methodName = methodAccess.Name.Identifier.Text;
        
        // Check for String.Method or System.String.Method
        // Heuristic: "String" or "string"
        if (typeExpression != "String" && typeExpression != "string" && typeExpression != "System.String")
            return false;
            
        return methodName switch
        {
            "IsNullOrEmpty" => true,
            "IsNullOrWhiteSpace" => true,
            "Join" => true,
            "Concat" => true,
            "Format" => true,
            "Compare" => true,
            "Equals" => true,
            _ => false
        };
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        if (node is MemberAccessExpressionSyntax { Name.Identifier.Text: "Empty" })
            return "''";

        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var methodName = memberAccess.Name.Identifier.Text;
        var args = invocation.ArgumentList.Arguments;

        if (methodName == "IsNullOrEmpty")
        {
            var target = context.Converter.ConvertExpression(args[0].Expression);
            return $"!{target}";
        }
        
        if (methodName == "IsNullOrWhiteSpace")
        {
            var target = context.Converter.ConvertExpression(args[0].Expression);
            // !x || !x.trim()
            return $"(!{target} || !{target}.trim())";
        }
        
        if (methodName == "Join")
        {
            // Join(separator, values)
            var separator = context.Converter.ConvertExpression(args[0].Expression);
            var values = context.Converter.ConvertExpression(args[1].Expression);
            return $"{values}.join({separator})";
        }
        
        if (methodName == "Concat")
        {
            if (args.Count == 0) return "''";
            // string.Concat(a, b, c) -> a + b + c
            // But if it's an array, use join
            if (args.Count == 1)
            {
                var arg = context.Converter.ConvertExpression(args[0].Expression);
                return $"[...{arg}].join('')";
            }
            var concatenated = string.Join(" + ", args.Select(a => context.Converter.ConvertExpression(a.Expression)));
            return $"({concatenated})";
        }

        if (methodName == "Format")
        {
             // Track L D11/EQ2100: when the TEMPLATE is a resx accessor it is per-culture DATA —
             // validated here, at build, against the neutral resx, because a format that will not
             // survive the trip to the browser must be a compile error, never a runtime surprise.
             if (context.SemanticHelper.GetSymbol(args[0].Expression) is IPropertySymbol templateProperty
                 && Services.ResourceClasses.IsResourceAccessor(templateProperty))
             {
                 ValidateResourceTemplate((InvocationExpressionSyntax)node, args, templateProperty, context);
             }

             // Route to the runtime helper, which substitutes {i}/{i:spec} (the latter via the same
             // formatter the interpolation path uses, so `{0:F2}` works) and unescapes {{/}}.
             context.UsedHelpers.Add(Eq.Import);
             var fmt = context.Converter.ConvertExpression(args[0].Expression);
             var restArgs = string.Join(", ", args.Skip(1).Select(a => context.Converter.ConvertExpression(a.Expression)));
             return restArgs.Length > 0 ? $"{Eq.StringFormat}({fmt}, {restArgs})" : $"{Eq.StringFormat}({fmt})";
        }

        if (methodName == "Compare")
        {
            if (args.Count < 2) return "0";
            // string.Compare(a, b) -> a.localeCompare(b)
            var first = context.Converter.ConvertExpression(args[0].Expression);
            var second = context.Converter.ConvertExpression(args[1].Expression);
            return $"{first}.localeCompare({second})";
        }

        if (methodName == "Equals")
        {
            if (args.Count < 2) return "false";
            // string.Equals(a, b) -> a === b
            // string.Equals(a, b, StringComparison.OrdinalIgnoreCase) -> a.toLowerCase() === b.toLowerCase()
            var first = context.Converter.ConvertExpression(args[0].Expression);
            var second = context.Converter.ConvertExpression(args[1].Expression);

            if (args.Count >= 3)
            {
                var comparison = args[2].Expression.ToString();
                if (comparison.Contains("IgnoreCase"))
                    return $"({first}.toLowerCase() === {second}.toLowerCase())";
            }
            return $"({first} === {second})";
        }

        return node.ToString();
    }

    public int Priority => 20;

    /// <summary>
    /// The M0 slice of the EQ2100 gate (docs/I18N-PLAN.md D7/D11): a resx template may use plain
    /// positional placeholders over STRING arguments — {0}, {1}, {{ }} escapes — and nothing else.
    /// Alignment ({0,10}) and format specifiers ({0:C}) wait for the M2 mapped subset; a non-string
    /// argument is refused too, because .NET formats it per culture and the browser would not —
    /// refused at build, never approximated at runtime.
    /// </summary>
    private static void ValidateResourceTemplate(
        InvocationExpressionSyntax node,
        IReadOnlyList<ArgumentSyntax> args,
        IPropertySymbol templateProperty,
        ConversionContext context)
    {
        for (var i = 1; i < args.Count; i++)
        {
            var argType = context.SemanticModel?.GetTypeInfo(args[i].Expression).Type;
            if (argType is not null && argType.SpecialType != SpecialType.System_String)
            {
                context.Report(args[i], ConversionSeverity.Error, "EQ2100",
                    $"string.Format over a resx template: argument {i - 1} is {argType.ToDisplayString()}, "
                    + "and a non-string value formats per culture in .NET but not in the browser. "
                    + "Pass a preformatted string until the M2 formatting subset lands.");
            }
        }

        var designerPath = Services.ResourceClasses.DesignerPathFor(templateProperty.ContainingType);
        var neutralPath = Services.ResxFiles.NeutralPathFor(designerPath);
        if (neutralPath is null) return;
        var values = Services.ResxFiles.Read(neutralPath);
        var key = Services.ResourceClasses.KeyFor(templateProperty);
        if (values is null || !values.TryGetValue(key, out var template)) return;

        var argCount = args.Count - 1;
        for (var i = 0; i < template.Length; i++)
        {
            var c = template[i];
            if (c == '{' && i + 1 < template.Length && template[i + 1] == '{') { i++; continue; }
            if (c == '}' && i + 1 < template.Length && template[i + 1] == '}') { i++; continue; }
            if (c == '}')
            {
                context.Report(node, ConversionSeverity.Error, "EQ2100",
                    $"resx template '{key}': a lone '}}' is not a valid composite format.");
                return;
            }
            if (c != '{') continue;

            var close = template.IndexOf('}', i + 1);
            if (close < 0)
            {
                context.Report(node, ConversionSeverity.Error, "EQ2100",
                    $"resx template '{key}': unterminated placeholder.");
                return;
            }
            var token = template[(i + 1)..close];
            if (token.Contains(',') || token.Contains(':'))
            {
                context.Report(node, ConversionSeverity.Error, "EQ2100",
                    $"resx template '{key}': '{{{token}}}' uses alignment or a format specifier, "
                    + "which is outside the v1 subset (plain positional only). The M2 mapped "
                    + "subset widens this; until then format on the server or split the string.");
                return;
            }
            if (!int.TryParse(token, out var index))
            {
                context.Report(node, ConversionSeverity.Error, "EQ2100",
                    $"resx template '{key}': '{{{token}}}' is not a positional placeholder.");
                return;
            }
            if (index >= argCount)
            {
                context.Report(node, ConversionSeverity.Error, "EQ2100",
                    $"resx template '{key}' expects argument {{{index}}} but the call passes only "
                    + $"{argCount}. The neutral resx is the arity contract every culture follows.");
                return;
            }
            i = close;
        }
    }
}
