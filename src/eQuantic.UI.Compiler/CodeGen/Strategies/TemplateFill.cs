using System.Text.RegularExpressions;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// Fills a JS emission template's <c>{0}</c>…<c>{9}</c> slots in ONE pass. Not string.Format:
/// templates legitimately carry literal braces (arrow bodies), and a substituted part must never
/// be re-scanned for placeholders — a user's string literal may contain <c>{0}</c> of its own.
/// </summary>
internal static class TemplateFill
{
    public static string With(string template, params string[] parts) =>
        Regex.Replace(template, @"\{(\d)\}", match => parts[int.Parse(match.Groups[1].Value)]);
}
