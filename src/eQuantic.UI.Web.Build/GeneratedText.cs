using System.Text;

namespace eQuantic.UI.Web.Build;

/// <summary>
/// The line break every generator in this assembly writes: LF, on every host.
/// <para>
/// <c>StringBuilder.AppendLine</c> appends <see cref="System.Environment.NewLine"/>, so these
/// generators wrote CRLF on Windows and LF everywhere else — and what they write is COMMITTED and
/// read back by bun, by the TypeScript specs and by pins that compare byte for byte. Six of those
/// pins were red on the Windows runner for exactly this reason, and green on the two hosts anyone
/// had run them on.
/// </para>
/// <para>
/// The rule already existed one assembly over — <c>CodeWriter</c> states it and gives the reason —
/// and these three generators were the ones reaching past it for a raw <see cref="StringBuilder"/>.
/// Moving them onto <c>CodeWriter</c> itself is the structural answer and a refactor of its own;
/// this makes them obey the same constant in the meantime, byte for byte identical to what they
/// already produced on a Unix host.
/// </para>
/// </summary>
internal static class GeneratedText
{
    private const char LineBreak = '\n';

    /// <summary>Appends <paramref name="line"/> and the SDK's own line break.</summary>
    internal static StringBuilder AppendLf(this StringBuilder builder, string line) =>
        builder.Append(line).Append(LineBreak);

    /// <summary>Appends <paramref name="value"/> and the SDK's own line break.</summary>
    internal static StringBuilder AppendLf(this StringBuilder builder, char value) =>
        builder.Append(value).Append(LineBreak);
}
