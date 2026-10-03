using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// <c>Enum.Parse</c>, <c>TryParse</c>, <c>GetNames</c>, <c>GetValues</c> and <c>IsDefined</c>, each
/// a call of the runtime's enum functions (<c>utils/enums.ts</c>) with the enum's shape written
/// inline (<see cref="EnumShape"/>): an enum has no object of its own in the browser, and these named
/// one after it, <c>Status</c>, which no module declares, so every one of them threw (#480). The enum
/// is the one the bound call names, by its type argument or by its <c>typeof</c>, and each argument
/// is the parameter it binds to, so a named one written out of order fills its own.
/// </summary>
public class EnumMethodStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;

        var name = memberAccess.Name.Identifier.Text;
        if (name is not ("Parse" or "TryParse" or "GetValues" or "GetNames" or "IsDefined")) return false;

        return context.ReceiverIsType(memberAccess.Expression,
            named => named.SpecialType == SpecialType.System_Enum,
            "Enum", "System.Enum");
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var name = ((MemberAccessExpressionSyntax)invocation.Expression).Name.Identifier.Text;
        if (context.SemanticHelper.GetOperation(invocation) is not IInvocationOperation call
            || EnumOf(call, context) is not { } enumType)
            return context.Unhandled(invocation, $"Enum.{name} over an enum the model cannot name");

        var shape = EnumShape.Of(enumType);
        context.UsedHelpers.Add(Eq.Import);
        ExpressionSyntax? Argument(string parameter) =>
            call.Arguments.FirstOrDefault(argument => argument.Parameter?.Name == parameter
                && argument.ArgumentKind == ArgumentKind.Explicit)?.Value.Syntax as ExpressionSyntax;
        string Converted(ExpressionSyntax expression) => context.Converter.ConvertExpression(expression);
        string IgnoreCase() => Argument("ignoreCase") is { } ignoreCase ? $", {Converted(ignoreCase)}" : "";

        switch (name)
        {
            case "GetNames":
                return $"{Eq.EnumNames}({shape})";
            case "GetValues":
                return $"{Eq.EnumValues}({shape})";
            case "Parse" when Argument("value") is { } text:
                return $"{Eq.EnumParse}({Converted(text)}, {shape}{IgnoreCase()})";
            case "IsDefined" when Argument("value") is { } value:
                // Given as the enum, as a number, or by its declared name: the argument's own type
                // says which, since a member's camelCase key and its name are both strings here.
                var given = context.SemanticHelper.GetType(value);
                var mode = given?.SpecialType == SpecialType.System_String
                    ? "name"
                    : given is INamedTypeSymbol { TypeKind: TypeKind.Enum } ? "held" : "number";
                return $"{Eq.EnumIsDefined}({Converted(value)}, {shape}, '{mode}')";
            case "TryParse" when Argument("value") is { } input
                && call.Arguments.FirstOrDefault(argument => argument.Parameter?.RefKind == RefKind.Out)?.Syntax
                    is ArgumentSyntax result:
                return TryParse(input, result, $"{Eq.EnumTryParse}({{0}}, {shape}{IgnoreCase()})", shape, context);
            default:
                return context.Unhandled(invocation, $"Enum.{name}");
        }
    }

    /// <summary>
    /// <c>Enum.TryParse</c>: true with the value in its out argument, or false with the enum's default
    /// there, as .NET leaves it. The out argument is the shared one's (<see cref="OutArgument"/>): a
    /// discard receives nothing, and a target with an effect of its own is written once.
    /// </summary>
    private static string TryParse(ExpressionSyntax input, ArgumentSyntax result, string parse, string shape,
        ConversionContext context)
    {
        var text = context.Converter.ConvertIr(input);
        if (OutArgument.IsDiscard(result, context))
            return JsExprWriter.Write(JsExpr.Template($"({parse} !== undefined)", [text], context.TypeAnnotations));
        var target = OutArgument.Target(result, context);
        var zero = $"{Eq.EnumZero}({shape})";
        if (OutArgument.IsBareName(target))
            return JsExprWriter.Write(JsExpr.Template(
                $"(({target} = {parse}) !== undefined || (({target} = {zero}), false))", [text], context.TypeAnnotations));
        var parts = new List<JsExpr> { text };
        var place = OutArgument.Place(result, context, part =>
        {
            parts.Add(part);
            return $"{{{parts.Count - 1}}}";
        });
        var value = context.TypeAnnotations ? "($r: any)" : "($r)";
        return JsExprWriter.Write(JsExpr.Template(
            $"({value} => ($r !== undefined ? (({place} = $r), true) : (({place} = {zero}), false)))({parse})",
            parts, context.TypeAnnotations));
    }

    /// <summary>The enum a call names: its type argument (<c>Parse&lt;Status&gt;</c>), or the type its
    /// <c>Type</c> argument is the <c>typeof</c> of (<c>Parse(typeof(Status), text)</c>).</summary>
    private static INamedTypeSymbol? EnumOf(IInvocationOperation call, ConversionContext context)
    {
        if (call.TargetMethod.TypeArguments is [INamedTypeSymbol { TypeKind: TypeKind.Enum } generic, ..])
            return generic;
        return call.Arguments.FirstOrDefault(argument => argument.Parameter?.Name == "enumType")?.Value
            is ITypeOfOperation { TypeOperand: INamedTypeSymbol { TypeKind: TypeKind.Enum } named }
            ? named
            : null;
    }

    public int Priority => 10;
}
