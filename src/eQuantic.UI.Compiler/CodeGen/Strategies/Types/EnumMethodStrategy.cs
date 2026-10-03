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

        // Each argument converts once, in the order it is written, which is the order C# evaluates
        // it in, and its hole names the parameter it binds to: `Parse(ignoreCase: NextBool(), value:
        // NextText())` runs NextBool first, where placing the value first ran it second. The template
        // writer keeps the parts in that order wherever the holes follow another.
        var parts = new List<JsExpr>();
        var holes = new Dictionary<string, (string Hole, ExpressionSyntax Value)>(StringComparer.Ordinal);
        ArgumentSyntax? result = null;
        foreach (var argument in call.Arguments
                     .Where(argument => argument.ArgumentKind == ArgumentKind.Explicit && argument.Parameter is not null)
                     .OrderBy(argument => argument.Syntax.SpanStart))
        {
            var parameter = argument.Parameter!;
            if (parameter.Name == "enumType") continue; // a typeof, which runs nothing
            if (parameter.RefKind == RefKind.Out)
            {
                result = argument.Syntax as ArgumentSyntax;
                continue;
            }
            if (argument.Value.Syntax is not ExpressionSyntax value) continue;
            holes[parameter.Name] = ($"{{{parts.Count}}}", value);
            parts.Add(context.Converter.ConvertIr(value));
        }
        string IgnoreCase() => holes.TryGetValue("ignoreCase", out var ignoreCase) ? $", {ignoreCase.Hole}" : "";
        string Write(string template) => JsExprWriter.Write(JsExpr.Template(template, parts, context.TypeAnnotations));

        switch (name)
        {
            case "GetNames":
                return $"{Eq.EnumNames}({shape})";
            case "GetValues":
                return $"{Eq.EnumValues}({shape})";
            case "Parse" when holes.TryGetValue("value", out var text):
                return Write($"{Eq.EnumParse}({text.Hole}, {shape}{IgnoreCase()})");
            case "IsDefined" when holes.TryGetValue("value", out var given):
                // Given as the enum, as a number, or by its declared name: the argument's own type
                // says which, since a member's camelCase key and its name are both strings here. An
                // `object` may hold any of the three, and the runtime asks the value.
                var mode = context.SemanticHelper.GetType(given.Value) switch
                {
                    { SpecialType: SpecialType.System_String } => "name",
                    INamedTypeSymbol { TypeKind: TypeKind.Enum } => "held",
                    { SpecialType: SpecialType.System_SByte or SpecialType.System_Byte or SpecialType.System_Int16
                        or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32
                        or SpecialType.System_Int64 or SpecialType.System_UInt64 } => "number",
                    _ => "object",
                };
                return Write($"{Eq.EnumIsDefined}({given.Hole}, {shape}, '{mode}')");
            case "TryParse" when holes.TryGetValue("value", out var input) && result is not null:
            {
                // True with the value in the out argument, or false with what .NET leaves there: the
                // generic overload's default, and null for the one that takes a Type. A discard
                // receives nothing, and a bare name is assigned in place.
                var failed = call.TargetMethod.IsGenericMethod ? $"{Eq.EnumZero}({shape})" : "null";
                var parse = JsExpr.Template($"{Eq.EnumTryParse}({input.Hole}, {shape}{IgnoreCase()})", parts,
                    context.TypeAnnotations);
                string Answer(string template, List<JsExpr> answerParts) =>
                    JsExprWriter.Write(JsExpr.Template(template, answerParts, context.TypeAnnotations));
                if (OutArgument.IsDiscard(result, context)) return Answer("({0} !== undefined)", [parse]);
                var target = OutArgument.Target(result, context);
                if (OutArgument.IsBareName(target))
                    return Answer($"(({target} = {{0}}) !== undefined || (({target} = {failed}), false))", [parse]);

                // A place that reads parts of its own (`slots[i]`) reads them where it is written: a
                // value written after it that reassigns `i` must not move the write. The parse's
                // answer is a part used twice, so the writer binds it, and the parts before it with it.
                var outer = new List<JsExpr>();
                var placeFirst = result.SpanStart < input.Value.SpanStart;
                if (!placeFirst) outer.Add(parse);
                var place = OutArgument.Place(result, context, part =>
                {
                    outer.Add(part);
                    return $"{{{outer.Count - 1}}}";
                });
                var found = placeFirst ? $"{{{outer.Count}}}" : "{0}";
                if (placeFirst) outer.Add(parse);
                return Answer($"({found} !== undefined ? (({place} = {found}), true) : (({place} = {failed}), false))", outer);
            }
            default:
                return context.Unhandled(invocation, $"Enum.{name}");
        }
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
