using System.Text.Encodings.Web;
using System.Text.Json;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// How every COMMITTED JSON fixture in this project is written, in one place because the reason is
/// one reason.
/// <para>
/// A fixture is generated here and asserted against the file in the repository, so the bytes have
/// to be the same on every host. <see cref="JsonWriterOptions.NewLine"/> defaults to
/// <see cref="Environment.NewLine"/>, which makes an indented document CRLF on Windows and LF
/// everywhere else — and the pin then fails on a Windows checkout for a difference no diff shows.
/// Measured: four of these fixtures failed exactly that way the first time the suite ran on a
/// Windows runner. <c>.gitattributes</c> already keeps the working tree LF; this is the other half,
/// because what the writer produces is not something git ever sees.
/// </para>
/// <para>
/// The trailing newline belongs here too: every one of these files ends with one, and a serializer
/// does not write it.
/// </para>
/// </summary>
internal static class FixtureJson
{
    /// <summary>Indented, LF, and the default escaping.</summary>
    internal static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        NewLine = "\n",
    };

    /// <summary>The same, for a fixture whose POINT is the characters — calendar names, where
    /// escaping every accent and every CJK glyph would make the file unreadable and its diffs
    /// meaningless.</summary>
    internal static readonly JsonSerializerOptions Readable = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Serialized the way this repository commits a fixture: indented, LF, one trailing
    /// newline.</summary>
    internal static string Write<T>(T value, JsonSerializerOptions? options = null) =>
        JsonSerializer.Serialize(value, options ?? Options) + "\n";
}
