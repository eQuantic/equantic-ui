using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// .NET APIs whose behaviour the BROWSER already has natively, under another name — URL escaping and
/// UTF-8 byte counting. There is nothing to reimplement here: <c>Uri.EscapeDataString</c> IS
/// <c>encodeURIComponent</c>, and <c>Encoding.UTF8</c> IS <c>TextEncoder</c>. Left untranslated they
/// reached the browser as <c>Uri.escapeDataString(…)</c> and threw.
/// </summary>
public class WebPrimitiveStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;

        var name = memberAccess.Name.Identifier.Text;
        var receiver = memberAccess.Expression.ToString();

        if (receiver is "Uri" or "System.Uri")
        {
            return name is "EscapeDataString" or "UnescapeDataString"
                or "EscapeUriString" or "UnescapeUriString";
        }

        // `Encoding.UTF8.GetBytes(s)` — the receiver is itself a member access.
        return IsUtf8(memberAccess.Expression)
               && name is "GetBytes" or "GetString" or "GetByteCount";
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var name = memberAccess.Name.Identifier.Text;
        var arg = invocation.ArgumentList.Arguments.Count > 0
            ? context.Converter.ConvertExpression(invocation.ArgumentList.Arguments[0].Expression)
            : "''";

        if (IsUtf8(memberAccess.Expression))
        {
            return name switch
            {
                // A .NET byte[] is a Uint8Array here — same bytes, same indexing, same length.
                "GetBytes" => $"new TextEncoder().encode({arg})",
                "GetByteCount" => $"new TextEncoder().encode({arg}).length",
                _ => $"new TextDecoder().decode({arg})",
            };
        }

        return name switch
        {
            "EscapeDataString" => $"encodeURIComponent({arg})",
            "UnescapeDataString" => $"decodeURIComponent({arg})",
            "EscapeUriString" => $"encodeURI({arg})",
            _ => $"decodeURI({arg})",
        };
    }

    private static bool IsUtf8(ExpressionSyntax expression) =>
        expression.ToString() is "Encoding.UTF8" or "System.Text.Encoding.UTF8"
            or "Encoding.ASCII" or "System.Text.Encoding.ASCII";

    public int Priority => 12;
}
