using System.Text;
using eQuantic.UI.Codegen;

namespace eQuantic.UI.Web.Build;

/// <summary>
/// One TypeScript string union, wrapped the way a diff can be read.
///
/// <para>
/// Two generators write these — the vocabulary's enums and its node kinds — and the wrapping is not
/// cosmetic: a union of forty members on one line is a diff nobody reviews, and a union of three
/// spread over four is noise. So it stays on one line while it fits in 100 columns and breaks into
/// <c>| member</c> continuations when it does not.
/// </para>
///
/// <para>
/// Shared rather than copied, because the SECOND generator is where a formatter starts to drift:
/// both files are byte-pinned, so a divergence here would be two pins disagreeing about the same
/// question with nothing saying which is right.
/// </para>
/// </summary>
internal static class TsUnion
{
    /// <summary>The column a union wraps at — the width the rest of the runtime's TypeScript is
    /// formatted to.</summary>
    private const int Width = 100;

    /// <summary>Writes <c>export type Name = 'a' | 'b';</c>, quoting each member.</summary>
    public static void Write(CodeWriter ts, string name, IEnumerable<string> members)
    {
        var quoted = members.Select(member => $"'{member}'").ToList();

        var oneLine = $"export type {name} = {string.Join(" | ", quoted)};";
        if (oneLine.Length <= Width)
        {
            ts.AppendLine(oneLine);
            return;
        }

        ts.AppendLine($"export type {name} =");
        var line = new StringBuilder("  "); // LINE ONLY
        foreach (var member in quoted)
        {
            var piece = line.Length == 2 ? member : $" | {member}";
            if (line.Length + piece.Length > Width)
            {
                ts.AppendLine(line.ToString());
                line = new StringBuilder("  | " + member); // LINE ONLY
                continue;
            }
            line.Append(piece);
        }
        ts.AppendLine(line.Append(';').ToString());
    }
}
