using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

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
            return FormatCall((InvocationExpressionSyntax)node, args, context);

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
    /// <summary>
    /// <c>string.Format</c>, its arguments bound by the method C# chose (#377): the provider, the
    /// template and the values, a params array passed whole spread as C#'s normal form reads it.
    /// The provider never reaches the browser, where <c>CultureInfo</c> does not exist: taken for the
    /// template, it made the page throw "CultureInfo is not defined". The invariant culture formats
    /// invariantly, the current culture as a call with none does, and any other is EQ2108, the
    /// policy <c>ToString</c> has. A float value is boxed with its kind, as C# boxes it into the
    /// object it is passed as, so the formatter writes a float's own digits (#378).
    /// </summary>
    private static string FormatCall(InvocationExpressionSyntax node, SeparatedSyntaxList<ArgumentSyntax> args,
        ConversionContext context)
    {
        ExpressionSyntax? provider = null, template = null;
        var values = new List<ExpressionSyntax>();
        var spread = false;
        if (context.SemanticHelper.GetSymbol(node) is IMethodSymbol { Parameters.Length: > 0 } method)
        {
            var last = method.Parameters[^1];
            for (var i = 0; i < args.Count; i++)
            {
                var named = args[i].NameColon?.Name.Identifier.ValueText;
                var parameter = named is not null
                    ? method.Parameters.FirstOrDefault(p => p.Name == named)
                    : i < method.Parameters.Length - 1 ? method.Parameters[i] : last;
                if (parameter is null) continue;
                if (parameter.Type is { Name: "IFormatProvider", ContainingNamespace.Name: "System" }) provider = args[i].Expression;
                else if (parameter.Name == "format") template = args[i].Expression;
                else values.Add(args[i].Expression);
            }
            // A params array passed as the array itself: its elements are the values. The bound call
            // says which form C# chose, so a covariant `string[]` and a collection expression are
            // the array too, where comparing the argument's type with the parameter's saw only an
            // exact `object[]` and formatted the others as one value.
            spread = context.SemanticHelper.GetOperation(node) is IInvocationOperation invocation
                && invocation.Arguments.Any(argument =>
                    argument.Parameter is { IsParams: true } && argument.ArgumentKind == ArgumentKind.Explicit);
            // An array WRITTEN IN PLACE is its elements: they are the values, each boxed as C#
            // boxes it into the array, so a float in `new object[] { 0.1f }` keeps its own digits.
            if (spread && ElementsOf(values[0]) is { } elements)
            {
                values = elements.ToList();
                spread = false;
            }
            if (template is null || context.SemanticHelper.GetType(template) is not { SpecialType: SpecialType.System_String })
                return context.Unhandled(node, "string.Format over a CompositeFormat");
        }
        else
        {
            template = args[0].Expression;
            values.AddRange(args.Skip(1).Select(a => a.Expression));
        }

        var function = Eq.StringFormat;
        if (provider is not null)
        {
            if (NamedCulture.IsInvariant(provider, context)) function = Eq.StringFormatInvariant;
            else if (!NamedCulture.IsCurrent(provider, context))
            {
                context.Report(node, ConversionSeverity.Error, "EQ2108",
                    "Only CultureInfo.InvariantCulture and CultureInfo.CurrentCulture cross to JavaScript. "
                    + "Format with one of them, or with no provider to follow the app's culture.");
                return "''";
            }
        }

        // Track L D11/EQ2100: when the TEMPLATE is a resx accessor it is per-culture DATA —
        // validated here, at build, against the neutral resx, because a format that will not
        // survive the trip to the browser must be a compile error, never a runtime surprise.
        if (context.SemanticHelper.GetSymbol(template) is IPropertySymbol templateProperty
            && Services.ResourceClasses.IsResourceAccessor(templateProperty))
        {
            ValidateResourceTemplate(node, spread ? int.MaxValue : values.Count, templateProperty, context);
        }

        // Route to the runtime helper, which substitutes {i}/{i,width}/{i:spec} (the spec through the
        // same formatter the interpolation path uses, so `{0:F2}` works) and unescapes {{/}}.
        context.UsedHelpers.Add(Eq.Import);
        var fmt = context.Converter.ConvertExpression(template);
        var rest = values.Select(value =>
        {
            var text = context.Converter.ConvertExpression(value);
            if (spread) return $"...{text}";
            return context.SemanticHelper.GetType(value).UnwrapNullable() is { SpecialType: SpecialType.System_Single }
                ? $"{Eq.AsSingle}({text})"
                : text;
        }).ToList();
        return rest.Count > 0 ? $"{function}({fmt}, {string.Join(", ", rest)})" : $"{function}({fmt})";
    }

    /// <summary>The values an array passed as the params array holds when it is written in place:
    /// they are the call's values, as a list of arguments would be, and a resx template's arity is
    /// held against them. Null where only the running program knows, a spread element included.</summary>
    private static IReadOnlyList<ExpressionSyntax>? ElementsOf(ExpressionSyntax array) => array switch
    {
        ArrayCreationExpressionSyntax { Initializer: { } initializer } => initializer.Expressions,
        ImplicitArrayCreationExpressionSyntax { Initializer: var initializer } => initializer.Expressions,
        CollectionExpressionSyntax collection when collection.Elements.All(element => element is ExpressionElementSyntax) =>
            collection.Elements.Cast<ExpressionElementSyntax>().Select(element => element.Expression).ToList(),
        _ => null,
    };

    private static void ValidateResourceTemplate(
        InvocationExpressionSyntax node,
        int argCount,
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
