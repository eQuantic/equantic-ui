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

        var methodName = methodAccess.Name.Identifier.Text;

        // The receiver must BE System.String — a user type merely named String must not route here.
        if (!context.ReceiverIsType(methodAccess.Expression,
                named => named.SpecialType == SpecialType.System_String,
                "String", "string", "System.String"))
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

        return context.Unhandled(node, "static String");
    }

    public int Priority => 20;

    /// <summary>
    /// The EQ2100/EQ2101 gate over a resx template (docs/I18N-PLAN.md D7/D11).
    ///
    /// EQ2100 is about THIS call: the template must be a valid composite format whose specifiers
    /// the browser can reproduce exactly (<see cref="Services.FormatSubset"/>), and it must not ask
    /// for an argument the call does not pass.
    ///
    /// EQ2101 is about the TRANSLATIONS: every culture's resx is checked against the neutral one,
    /// because a pt-BR string that says {2} where the neutral says {0}/{1} is a crash a Brazilian
    /// visitor finds, on a page nobody tested — the build machine is where that belongs.
    /// </summary>
    private static void ValidateResourceTemplate(
        InvocationExpressionSyntax node,
        IReadOnlyList<ArgumentSyntax> args,
        IPropertySymbol templateProperty,
        ConversionContext context)
    {
        var designerPath = Services.ResourceClasses.DesignerPathFor(templateProperty.ContainingType);
        var neutralPath = Services.ResxFiles.NeutralPathFor(designerPath);
        if (neutralPath is null) return;
        var values = Services.ResxFiles.Read(neutralPath);
        var key = Services.ResourceClasses.KeyFor(templateProperty);
        if (values is null || !values.TryGetValue(key, out var template)) return;

        var holes = Services.FormatSubset.Read(template, out var error);
        if (holes is null)
        {
            context.Report(node, ConversionSeverity.Error, "EQ2100",
                $"resx template '{key}': {error}.");
            return;
        }

        var argCount = args.Count - 1;
        foreach (var hole in holes)
        {
            if (hole.Index < argCount) continue;
            context.Report(node, ConversionSeverity.Error, "EQ2100",
                $"resx template '{key}' expects argument {{{hole.Index}}} but the call passes only "
                + $"{argCount}. The neutral resx is the arity contract every culture follows.");
            return;
        }

        ValidateCultureTemplates(node, key, designerPath, holes, context);
    }

    /// <summary>
    /// EQ2101: every OTHER culture's template for this key, held against the neutral one. A
    /// translator works in a file the compiler never reads at the call site, so this is the only
    /// place the two can be compared — and an arity drift there is a runtime error in exactly one
    /// language.
    /// </summary>
    private static void ValidateCultureTemplates(
        InvocationExpressionSyntax node,
        string key,
        string designerPath,
        IReadOnlyList<Services.TemplateHole> neutralHoles,
        ConversionContext context)
    {
        var expected = new SortedSet<int>(neutralHoles.Select(hole => hole.Index));

        foreach (var (culture, path) in Services.ResxFiles.VariantsFor(designerPath))
        {
            if (culture.Length == 0) continue; // the neutral one IS the contract
            var values = Services.ResxFiles.Read(path);
            if (values is null || !values.TryGetValue(key, out var template)) continue;

            var holes = Services.FormatSubset.Read(template, out var error);
            if (holes is null)
            {
                context.Report(node, ConversionSeverity.Error, "EQ2101",
                    $"resx template '{key}' in '{culture}': {error}.");
                continue;
            }

            var actual = new SortedSet<int>(holes.Select(hole => hole.Index));
            if (actual.SetEquals(expected)) continue;

            context.Report(node, ConversionSeverity.Error, "EQ2101",
                $"resx template '{key}' in '{culture}' uses "
                + (actual.Count == 0 ? "no placeholders" : "{" + string.Join("}, {", actual) + "}")
                + " but the neutral culture uses "
                + (expected.Count == 0 ? "none" : "{" + string.Join("}, {", expected) + "}")
                + ". A translation that asks for an argument the call never passes throws for the "
                + "readers of that language only.");
        }
    }
}
