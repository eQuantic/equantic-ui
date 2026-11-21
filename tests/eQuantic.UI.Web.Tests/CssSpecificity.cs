using System.Text.RegularExpressions;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The weight of the rules a sheet writes, read as the browser reads it, for the facts that decide a
/// state's order by its selector alone: the control's press, focus and hover (#616) and a header's
/// scroll under them (#1769). <see cref="PressedStateRealizerTests"/> pins the reader itself.
/// </summary>
internal static class CssSpecificity
{
    /// <summary>
    /// The selector that writes <paramref name="declaration"/> for one of <paramref name="element"/>'s
    /// classes, and its specificity as the browser computes it: (ids, classes and pseudo-classes and
    /// attributes, types), with <c>:where()</c> counting nothing, which every selector this sheet
    /// writes is made of.
    /// </summary>
    internal static (string Selector, (int, int, int) Specificity) RuleWriting(string css, HtmlElement element,
        string declaration)
    {
        foreach (var cls in (element.ClassName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            // After a closing brace, or the opening one of the hover family's @media gate.
            var rule = Regex.Match(css, $@"(?:^|[{{}}])([^{{}}@]*\.{Regex.Escape(cls)}(?:[:\[][^{{}}]*)?)\{{{declaration}\}}");
            if (!rule.Success) continue;
            var selector = rule.Groups[1].Value.Trim();
            selector.Should().NotContain(",", "one selector, whose specificity is the rule's");
            return (selector, Specificity(selector));
        }
        throw new InvalidOperationException($"no rule of the element writes {declaration}");
    }

    /// <summary>
    /// A selector's specificity, as Selectors Level 4 computes it: <c>:where()</c> weighs nothing,
    /// <c>:not()</c>, <c>:is()</c> and <c>:has()</c> weigh their heaviest argument, and the state
    /// families carry both (#616), so a regex count would not do.
    /// </summary>
    internal static (int, int, int) Specificity(string selector)
    {
        var at = 0;
        return Selector(selector, ref at);

        static (int, int, int) Selector(string text, ref int at)
        {
            int ids = 0, classes = 0, types = 0;
            var startOfCompound = true;
            while (at < text.Length && text[at] != ')' && text[at] != ',')
            {
                var c = text[at];
                if (c is ' ' or '>' or '+' or '~') { at++; startOfCompound = true; continue; }
                if (c == '#') { ids++; at++; Ident(text, ref at); }
                else if (c == '.') { classes++; at++; Ident(text, ref at); }
                else if (c == '[') { classes++; at = text.IndexOf(']', at) + 1; }
                else if (c == '*') at++;
                else if (c == ':' && at + 1 < text.Length && text[at + 1] == ':') { types++; at += 2; Ident(text, ref at); }
                else if (c == ':')
                {
                    at++;
                    var name = Ident(text, ref at);
                    if (at < text.Length && text[at] == '(')
                    {
                        at++;
                        var heaviest = (0, 0, 0);
                        while (true)
                        {
                            var argument = Selector(text, ref at);
                            if (Compare(argument, heaviest) > 0) heaviest = argument;
                            if (text[at] == ',') { at++; continue; }
                            at++; // ')'
                            break;
                        }
                        if (name is "not" or "is" or "has")
                        { ids += heaviest.Item1; classes += heaviest.Item2; types += heaviest.Item3; }
                        else if (name != "where") classes++;
                    }
                    else classes++;
                }
                else { if (startOfCompound) types++; Ident(text, ref at); }
                startOfCompound = false;
            }
            return (ids, classes, types);
        }

        static string Ident(string text, ref int at)
        {
            var from = at;
            while (at < text.Length && (char.IsLetterOrDigit(text[at]) || text[at] is '-' or '_')) at++;
            return text[from..at];
        }
    }

    internal static int Compare((int, int, int) a, (int, int, int) b) =>
        a.Item1 != b.Item1 ? a.Item1.CompareTo(b.Item1)
        : a.Item2 != b.Item2 ? a.Item2.CompareTo(b.Item2)
        : a.Item3.CompareTo(b.Item3);
}
