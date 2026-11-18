using System.Text;

namespace eQuantic.UI.Primitives;

/// <summary>
/// How a step of a sort is said (#1564): the holes of a <see cref="SortAnnouncements"/> text filled, written ONCE
/// for every realizer. The browser's controller runs its transpiled twin and the Photon host runs this, so the two
/// say an app's words alike, whatever they hold.
/// </summary>
public static class SortWords
{
    /// <summary>
    /// <paramref name="text"/> with each <c>{n}</c> it names replaced by the n-th of <paramref name="holes"/>, in
    /// one pass, so a hole's own value is never read as a hole (a file called <c>{1}.txt</c>). A <c>{n}</c> past
    /// the holes stays as it is written, and a brace that opens no hole is text, so an app's words can never fail
    /// a key press, as <c>string.Format</c> would on a stray brace.
    /// </summary>
    public static string Fill(string text, params string[] holes)
    {
        var filled = new StringBuilder(text.Length);
        var at = 0;
        while (at < text.Length)
        {
            var close = text[at] == '{' ? text.IndexOf('}', at + 1) : -1;
            var hole = close > at + 1 ? HoleBetween(text, at + 1, close) : -1;
            if (hole >= 0 && hole < holes.Length)
            {
                filled.Append(holes[hole]);
                at = close + 1;
                continue;
            }
            filled.Append(text[at]);
            at++;
        }
        return filled.ToString();
    }

    /// <summary>The number written between <paramref name="start"/> and <paramref name="end"/>, or -1 when it is
    /// not digits alone.</summary>
    private static int HoleBetween(string text, int start, int end)
    {
        var hole = 0;
        for (var at = start; at < end; at++)
        {
            var digit = text[at] - '0';
            if (digit < 0 || digit > 9) return -1;
            hole = hole * 10 + digit;
        }
        return hole;
    }
}
