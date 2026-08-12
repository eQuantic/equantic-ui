using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace eQuantic.UI.Web;

/// <summary>
/// Generates <c>sdk-strings.generated.ts</c> — the SDK's own NEUTRAL strings, baked into the
/// runtime (Track L D14).
/// <para>
/// This is a safety net with a precise job. Once the SDK's chrome moved behind a resx, a client
/// with no catalog installed would resolve <c>SdkStrings.SearchPlaceholder</c> to its KEY (the
/// missing-key policy) and a search field would read "SearchPlaceholder" instead of "Search…".
/// The catalog is normally there — the build emits it for every app and the shell inlines it —
/// but "normally" is not a contract: a page mounted without the server bridge, a test host, the
/// playground. So the neutral values ride the runtime itself, and the resolution order becomes
/// catalog → built-in neutral → key.
/// </para>
/// The same "generated, never hand-written" rule the tokens and glyphs follow: the C# resx is the
/// single source, and a test pins the output byte-for-byte.
/// </summary>
public static class SdkStringsTsGenerator
{
    /// <summary>Reads the neutral resx (the file itself is the source of truth) and emits the
    /// module. The path is passed in so the generator stays a pure function of its input.</summary>
    public static string Generate(string neutralResxPath)
    {
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in XDocument.Load(neutralResxPath).Root?.Elements("data") ?? [])
        {
            var name = entry.Attribute("name")?.Value;
            // A typed/binary entry is not a UI string — the same skip the catalog reader makes.
            if (name is null || entry.Attribute("type") is not null || entry.Attribute("mimetype") is not null)
                continue;
            values[name] = entry.Element("value")?.Value ?? "";
        }

        var builder = new StringBuilder();
        builder.Append("""
            /**
             * GENERATED — do not edit. The SDK's own neutral strings, from SdkResources.resx.
             * Regenerate: EQ_UPDATE_SDK_STRINGS_TS=1 dotnet test eQuantic.UI.Web.Tests.
             *
             * The LAST resort of `$eq.str`, never the first: an installed culture catalog always
             * wins, and this only keeps a page with no catalog at all reading English chrome
             * instead of raw keys.
             */

            export const sdkNeutralStrings: Record<string, string> = {

            """);
        foreach (var (key, value) in values)
            builder.Append($"  '{key}': {JsonSerializer.Serialize(value)},\n");
        builder.Append("};\n");
        return builder.ToString();
    }
}
