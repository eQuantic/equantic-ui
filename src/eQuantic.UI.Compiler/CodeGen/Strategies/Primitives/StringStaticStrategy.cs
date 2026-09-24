using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

/// <summary>
/// Strategy for static String methods.
/// Handles:
/// - String.IsNullOrEmpty(s) -> !s
/// - String.IsNullOrWhiteSpace(s) -> (!$eq.text.hasNonWhiteSpace(s)), .NET's white space
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
            // .NET's white space, and the argument read once: `!x || !x.trim()` read it twice and
            // trimmed with JavaScript's set, which leaves U+0085 and takes U+FEFF. The negation of a
            // one-sided predicate, so that past a false answer TypeScript knows a string, as C# does.
            context.UsedHelpers.Add(Eq.Import);
            return $"(!{Eq.HasNonWhiteSpace}({target}))";
        }
        
        if (methodName == "Join")
        {
            if (context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol
                {
                    Parameters: [_, _, { Type.SpecialType: SpecialType.System_Int32 }, { Type.SpecialType: SpecialType.System_Int32 }],
                } range)
            {
                return Call($"{Eq.StringJoinRange}({{0}}, {{1}}, {{2}}, {{3}})", invocation, range, context);
            }

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
            return ConcatCall(invocation, context);
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
            if (context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol compare)
                return CompareCall(invocation, compare, context);
            if (args.Count < 2) return "0";
            // string.Compare(a, b) -> a.localeCompare(b)
            var first = context.Converter.ConvertExpression(args[0].Expression);
            var second = context.Converter.ConvertExpression(args[1].Expression);
            return $"{first}.localeCompare({second})";
        }

        if (methodName == "Equals")
        {
            // With a comparison, by the one the call passes, which is a value like any other: read
            // from its SPELLING, a comparison held in a variable was ordinal, and a null threw.
            if (context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol { Parameters: [_, _, var comparisonType] } equals
                && comparisonType.Type.IsNamed("System.StringComparison"))
            {
                return Call($"{Eq.StringEquals}({{0}}, {{1}}, {{2}})", invocation, equals, context);
            }
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

    /// <summary>
    /// <c>string.Concat</c> of two values or more: each written into the text as .NET writes it
    /// (<see cref="StringConversion"/>), and the text JOINED. <c>(a + b)</c> added two numbers,
    /// <c>string.Concat(1, 2)</c> answering 3, and wrote a null as "null" and a bool in lower case.
    /// </summary>
    private static string ConcatCall(InvocationExpressionSyntax node, ConversionContext context)
    {
        var arguments = node.ArgumentList.Arguments;
        var parts = arguments
            .Select(argument => StringConversion.ToDotNetString(argument.Expression,
                context.Converter.ConvertIr(argument.Expression), context))
            .ToArray();
        var template = "''" + string.Concat(Enumerable.Range(0, parts.Length).Select(i => " + {" + i + "}"));
        if (context.SemanticHelper.GetSymbol(node) is IMethodSymbol method)
            template = PrimitiveStaticStrategy.BindNamedArguments(template, node, method);
        return JsExprWriter.Write(JsExpr.Template(template, parts, context.TypeAnnotations));
    }

    /// <summary>
    /// <c>string.Compare</c> by the overload C# bound. With no comparison named it is the current
    /// culture's, case-blind where a bool says so; with a <c>StringComparison</c>, that one; and over
    /// two RANGES where the call gives indexes and a length, each clamped and checked as .NET checks
    /// it. A null orders first. <c>localeCompare</c> over the first two arguments answered all of
    /// them: with a range it compared the first string with an INDEX, and it dropped a case flag and a
    /// comparison. A culture or <c>CompareOptions</c> passed in has no form this side reads, and is
    /// refused rather than dropped.
    /// </summary>
    private static string CompareCall(InvocationExpressionSyntax node, IMethodSymbol method, ConversionContext context)
    {
        var shape = string.Join(",", method.Parameters.Select(parameter => parameter.Type switch
        {
            { SpecialType: SpecialType.System_String } => "s",
            { SpecialType: SpecialType.System_Int32 } => "i",
            { SpecialType: SpecialType.System_Boolean } => "b",
            var type when type.IsNamed("System.StringComparison") => "c",
            _ => "?",
        }));
        var template = shape switch
        {
            "s,s" => $"{Eq.StringCompare}({{0}}, {{1}}, 'currentCulture')",
            "s,s,b" => $"{Eq.StringCompare}({{0}}, {{1}}, {{2}} ? 'currentCultureIgnoreCase' : 'currentCulture')",
            "s,s,c" => $"{Eq.StringCompare}({{0}}, {{1}}, {{2}})",
            "s,i,s,i,i" => $"{Eq.StringCompareRange}({{0}}, {{1}}, {{2}}, {{3}}, {{4}}, false)",
            "s,i,s,i,i,b" => $"{Eq.StringCompareRange}({{0}}, {{1}}, {{2}}, {{3}}, {{4}}, {{5}})",
            "s,i,s,i,i,c" => $"{Eq.StringCompareRangeBy}({{0}}, {{1}}, {{2}}, {{3}}, {{4}}, {{5}})",
            _ => null,
        };
        return template is null
            ? context.Unhandled(node, "string.Compare with a CultureInfo or CompareOptions")
            : Call(template, node, method, context);
    }

    /// <summary>A runtime helper over the call's arguments, each in its PARAMETER's hole and all of
    /// them evaluated in the order they were written.</summary>
    private static string Call(string template, InvocationExpressionSyntax node, IMethodSymbol method, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        var parts = node.ArgumentList.Arguments.Select(argument => context.Converter.ConvertIr(argument.Expression)).ToArray();
        return JsExprWriter.Write(JsExpr.Template(PrimitiveStaticStrategy.BindNamedArguments(template, node, method),
            parts, context.TypeAnnotations));
    }
}
