using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;
using eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Maps <c>System.Text.StringBuilder</c> to the runtime <c>StringBuilder</c> compat type.
/// <c>new StringBuilder(...)</c> becomes the <c>stringBuilder(...)</c> factory; instance methods
/// (<c>Append</c>, <c>AppendLine</c>, <c>Insert</c>, <c>Remove</c>, <c>Replace</c>, <c>Clear</c>,
/// <c>ToString</c>) and <c>Length</c> become their camelCase equivalents on the value, which take each
/// overload by its count of arguments. The <c>char[]</c> overloads of <c>Append</c> and <c>Insert</c>
/// are named for what they are (<c>appendChars</c>, <c>insertChars</c>): the runtime cannot tell a null
/// array from a null string, and .NET refuses the two in different words (#650).
/// <para>
/// <c>AppendFormat</c> and <c>AppendJoin</c> append what <c>string.Format</c> and <c>string.Join</c>
/// write, bound by the same parameters and under the same culture policy; <c>Equals(StringBuilder)</c>
/// is the runtime's <c>equalsBuilder</c>, since <c>Equals(object)</c> is identity; and the
/// <c>Chars</c> indexer is the twin's <c>item</c> and <c>setItem</c> (<see cref="CarriesIndexer"/>).
/// Each was a member the runtime did not have, a TypeError in the browser behind a green build (#679).
/// </para>
/// <para>
/// An interpolated <c>Append($"…")</c> or <c>AppendLine($"…")</c> appends each part in turn, as .NET's
/// interpolation handler does (<see cref="Interpolated"/>), and a call that names its arguments binds
/// each to its parameter, since the twin takes them by position.
/// </para>
/// </summary>
/// <remarks>
/// Priority 15 so it wins over the generic ToString (10), ObjectCreation (5) and member-access (0)
/// strategies for StringBuilder nodes. Gated on the semantic type (falls back to the type name when no
/// semantic model is present).
/// </remarks>
public class StringBuilderStrategy : ConversionStrategyBase
{
    private const string TypeName = "System.Text.StringBuilder";

    public override bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        switch (node)
        {
            case BaseObjectCreationExpressionSyntax oc:
                return IsType(context.SemanticHelper.GetType(oc))
                    || (oc is ObjectCreationExpressionSyntax named && named.Type.ToString() == "StringBuilder");

            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma }:
                return IsMember(ma, context);

            case MemberAccessExpressionSyntax member:
                return IsMember(member, context);

            default:
                return false;
        }
    }

    public override string Convert(SyntaxNode node, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        switch (node)
        {
            case BaseObjectCreationExpressionSyntax oc:
                if (Named(oc.ArgumentList) && context.SemanticHelper.GetSymbol(oc) is IMethodSymbol constructor)
                    return JsExprWriter.Write(ParameterTemplate.Construction(
                        $"{Eq.StringBuilder}({Holes(0, oc.ArgumentList!.Arguments.Count)})", oc, constructor, context));
                return $"{Eq.StringBuilder}({ConvertArgs(oc.ArgumentList, context)})";

            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma } inv:
            {
                var method = context.SemanticHelper.GetSymbol(ma) as IMethodSymbol;
                if (method is not null && Uncrossable(method, context.SemanticHelper.GetOperation(inv) as IInvocationOperation) is { } why)
                    return context.Unhandled(node, why);
                // The twin takes C#'s parameters by position, so a named argument written out of their
                // order (sb.CopyTo(count: 2, destination: a, …)) is bound to its parameter, each still
                // evaluated in the order written. The members below bind their own arguments.
                if (method is not null && Named(inv.ArgumentList) && !BindsItsOwnArguments(method))
                    return JsExprWriter.Write(ParameterTemplate.Call(
                        $"{{0}}.{RuntimeName(method, ma.Name.Identifier.Text)}({Holes(1, inv.ArgumentList.Arguments.Count)})",
                        context.Converter.ConvertIr(ma.Expression), inv, method, context));
                var receiver = context.Converter.ConvertExpression(ma.Expression);
                switch (method)
                {
                    case { Name: "AppendFormat" }:
                        return $"{receiver}.append({StringStaticStrategy.FormatCall(inv, inv.ArgumentList.Arguments, context)})";
                    case { Name: "AppendJoin" }:
                        return $"{receiver}.append({StringStaticStrategy.JoinCall(inv, method, context)})";
                    case { Name: "Append" or "AppendLine" } when Handled(method):
                        return Interpolated(receiver, method, inv, context);
                }
                var name = RuntimeName(method, ma.Name.Identifier.Text);
                return $"{receiver}.{name}({ConvertArgs(inv.ArgumentList, context)})";
            }

            case MemberAccessExpressionSyntax member:
            {
                var receiver = context.Converter.ConvertExpression(member.Expression);
                return $"{receiver}.{member.Name.Identifier.Text.ToCamelCase()}";
            }

            default:
                return context.Unhandled(node, "StringBuilder");
        }
    }

    /// <summary>The runtime method an overload is: its name in camelCase, the <c>char[]</c>
    /// overloads of <c>Append</c> and <c>Insert</c> as their own method, <c>Append(StringBuilder)</c>
    /// as <c>appendBuilder</c>, which .NET refuses in words a string's append does not use and the
    /// runtime cannot tell from <c>Append(object)</c> by its value, and <c>Equals(StringBuilder)</c>
    /// as <c>equalsBuilder</c>.</summary>
    private static string RuntimeName(IMethodSymbol? method, string name) => method switch
    {
        { Name: "Append" or "Insert" } when method.Parameters.Any(parameter => parameter is { Name: "value", Type: IArrayTypeSymbol })
            => $"{name.ToCamelCase()}Chars",
        { Name: "Append", Parameters: [{ Type: var value }, ..] } when IsType(value) => "appendBuilder",
        { Name: "Equals", Parameters: [{ Type: var other }] } when IsType(other) => "equalsBuilder",
        _ => name.ToCamelCase(),
    };

    /// <summary>Whether an overload takes .NET's interpolation handler, which an interpolated string
    /// binds: <c>Append($"…")</c> and <c>AppendLine($"…")</c>, with or without a provider.</summary>
    private static bool Handled(IMethodSymbol method) =>
        method.Parameters is [.., { Type.Name: "AppendInterpolatedStringHandler" }];

    /// <summary>
    /// <c>Append($"…")</c> and <c>AppendLine($"…")</c> as .NET's interpolation handler runs them: each
    /// part appended as it is reached, so a hole that reads the builder sees the parts before it, a
    /// refusal keeps them, and each part grows the chunks on its own (a hole's alignment pads in an
    /// append of its own, <c>appendAligned</c>). The whole text appended at once read every hole first:
    /// <c>$"{sb.Length}{sb.Length}"</c> was "00" where .NET writes "01". A provider is the current
    /// culture, which a null provider or <c>CultureInfo.CurrentCulture</c> names; any other is EQ2108,
    /// since a hole formats in the app's culture alone.
    /// </summary>
    private static string Interpolated(string receiver, IMethodSymbol method, InvocationExpressionSyntax invocation,
        ConversionContext context)
    {
        var handler = method.Parameters.Length - 1;
        if (ParameterTemplate.Filling(invocation, method, handler)?.Expression is not { } text)
            return context.Unhandled(invocation, "StringBuilder." + method.Name);
        if (handler == 1)
        {
            if (ParameterTemplate.Filling(invocation, method, 0)?.Expression is not { } provider)
                return context.Unhandled(invocation, "StringBuilder." + method.Name + " with a provider");
            if (!NamedCulture.IsCurrent(provider, context))
            {
                context.Report(invocation, ConversionSeverity.Error, "EQ2108",
                    "An interpolated string crosses to JavaScript in the app's culture alone. Pass "
                    + "CultureInfo.CurrentCulture or no provider, or append string.Format's text, which takes "
                    + "CultureInfo.InvariantCulture too.");
                return "''";
            }
        }
        var line = method.Name == "AppendLine" ? ".appendLine()" : "";
        var parts = Parts(text);
        if (parts is null)
            return $"{receiver}.{method.Name.ToCamelCase()}({context.Converter.ConvertExpression(text)})";
        var chain = new System.Text.StringBuilder(receiver);
        foreach (var (content, owner) in parts)
        {
            switch (content)
            {
                case InterpolatedStringTextSyntax literal:
                    chain.Append(".append(").Append(JsStringLiteral.Quote(InterpolatedStringStrategy.Text(literal, owner))).Append(')');
                    break;
                case InterpolationSyntax hole:
                    var value = InterpolatedStringStrategy.Hole(hole, context, padded: false);
                    chain.Append(hole.AlignmentClause is { } alignment
                        ? $".appendAligned({value}, {alignment.Value})"
                        : $".append({value})");
                    break;
            }
        }
        return chain.Append(line).ToString();
    }

    /// <summary>The parts of an interpolated string in the order the handler appends them, through
    /// parentheses and the <c>+</c> that joins interpolated strings into one; null for any other
    /// expression, which is appended whole.</summary>
    private static List<(InterpolatedStringContentSyntax Content, InterpolatedStringExpressionSyntax Owner)>? Parts(
        ExpressionSyntax expression)
    {
        switch (expression)
        {
            case InterpolatedStringExpressionSyntax interpolated:
                return interpolated.Contents.Select(content => (content, interpolated)).ToList();
            case ParenthesizedExpressionSyntax parenthesized:
                return Parts(parenthesized.Expression);
            case BinaryExpressionSyntax sum when sum.IsKind(SyntaxKind.AddExpression)
                && Parts(sum.Left) is { } left && Parts(sum.Right) is { } right:
                return [.. left, .. right];
            default:
                return null;
        }
    }

    /// <summary>Why a member cannot cross, or null: <c>GetChunks</c>, whose chunks the browser's
    /// builder does not keep, and a span or a memory handed to an overload, which JavaScript does not
    /// have. A <c>params</c> span the call expands is the values written one by one, which .NET 9 binds
    /// <c>AppendJoin(",", "a", "b")</c> to, and crosses; a span passed whole to it does not.</summary>
    private static string? Uncrossable(IMethodSymbol method, IInvocationOperation? operation)
    {
        if (method.Name == "GetChunks") return "StringBuilder.GetChunks, whose chunks the browser's builder does not keep";
        foreach (var parameter in method.Parameters)
        {
            if (parameter.Type is not INamedTypeSymbol { Name: "Span" or "ReadOnlySpan" or "Memory" or "ReadOnlyMemory" })
                continue;
            var argument = operation?.Arguments.FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.Parameter, parameter));
            if (parameter.IsParams && argument?.ArgumentKind is ArgumentKind.ParamArray or ArgumentKind.ParamCollection)
                continue;
            return $"StringBuilder.{method.Name} over a span or a memory, which JavaScript does not have";
        }
        return null;
    }

    /// <summary>The members whose lowering binds its arguments by their parameters itself.</summary>
    private static bool BindsItsOwnArguments(IMethodSymbol method) =>
        method is { Name: "AppendFormat" or "AppendJoin" } || Handled(method);

    /// <summary>Whether a call names any of its arguments, which may then be written out of order.</summary>
    private static bool Named(BaseArgumentListSyntax? arguments) =>
        arguments?.Arguments.Any(argument => argument.NameColon is not null) == true;

    /// <summary>The holes of <paramref name="count"/> parameters, numbered from <paramref name="first"/>.</summary>
    private static string Holes(int first, int count) =>
        string.Join(", ", Enumerable.Range(first, count).Select(i => "{" + i + "}"));

    /// <summary>Whether an indexer is the builder's <c>Chars</c>, which the runtime's builder carries
    /// as <c>item</c> and <c>setItem</c>: read through JavaScript's subscript, <c>sb[0]</c> was a
    /// property no builder had, and a write set one nobody read (#679).</summary>
    internal static bool CarriesIndexer(IPropertySymbol indexer) => IsType(indexer.ContainingType);

    private static bool IsMember(MemberAccessExpressionSyntax ma, ConversionContext context)
    {
        var symbol = context.SemanticHelper.GetSymbol(ma);
        if (symbol?.ContainingType != null)
            return symbol.ContainingType.ToDisplayString() == TypeName;

        return IsType(context.SemanticHelper.GetType(ma.Expression));
    }

    /// <summary>Whether a type is the builder, a nullable reference to one included
    /// (<c>Equals(StringBuilder?)</c>'s parameter).</summary>
    private static bool IsType(ITypeSymbol? type) =>
        type?.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString() == TypeName;

    public override int Priority => 15;
}
