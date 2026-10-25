using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace eQuantic.UI.Build;

/// <summary>
/// <c>fonts.lock.json</c>, beside the project: the file of each cut a provider served the app, pinned by
/// its URL and the SHA-256 of its bytes the first time the build fetched it. The SDK writes it and the app
/// commits it, as it commits NuGet's <c>packages.lock.json</c>, so every build of the app sets its text in
/// the same bytes until the app takes a family out of it to renew it.
/// <code>
/// {
///   "version": 1,
///   "google": {
///     "Inter": {
///       "400": { "url": "https://fonts.gstatic.com/s/inter/v20/….ttf", "sha256": "1b08…" }
///     }
///   }
/// }
/// </code>
/// </summary>
public sealed class FontLock
{
    /// <summary>The file's name, beside the project.</summary>
    public const string FileName = "fonts.lock.json";

    private const int Version = 1;

    /// <summary>One cut's file: where it was fetched from, and the hash of its bytes.</summary>
    /// <param name="Url">The URL it was fetched from, the only one it is fetched from again.</param>
    /// <param name="Sha256">The SHA-256 of its bytes, in lowercase hex.</param>
    public sealed record Pin(Uri Url, string Sha256);

    private readonly SortedDictionary<string, SortedDictionary<string, SortedDictionary<FontCut, Pin>>> _providers =
        new(StringComparer.Ordinal);

    /// <summary>The pins of <paramref name="family"/> at <paramref name="provider"/>, none when it has none.</summary>
    public IReadOnlyDictionary<FontCut, Pin> Of(string provider, string family) =>
        _providers.TryGetValue(provider, out var families) && families.TryGetValue(family, out var cuts)
            ? cuts
            : new Dictionary<FontCut, Pin>();

    /// <summary>Pins <paramref name="family"/>'s cuts at <paramref name="provider"/>, in place of any it had.</summary>
    public void Set(string provider, string family, IReadOnlyDictionary<FontCut, Pin> cuts)
    {
        if (!_providers.TryGetValue(provider, out var families))
            _providers[provider] = families = new SortedDictionary<string, SortedDictionary<FontCut, Pin>>(StringComparer.Ordinal);
        families[family] = new SortedDictionary<FontCut, Pin>(cuts.ToDictionary());
    }

    /// <summary>A lock with every pin of this one but <paramref name="provider"/>'s, which a build sets again.</summary>
    public FontLock Without(string provider)
    {
        var rest = new FontLock();
        foreach (var (name, families) in _providers.Where(entry => entry.Key != provider))
            foreach (var (family, cuts) in families)
                rest.Set(name, family, cuts);
        return rest;
    }

    /// <summary>Whether the lock pins nothing at all.</summary>
    public bool IsEmpty => _providers.Values.All(families => families.Count == 0);

    /// <summary>
    /// The lock at <paramref name="path"/>, an empty one when there is none. A file that does not hold
    /// throws <see cref="InvalidDataException"/>, with the reason in words: it is never read as fewer pins.
    /// </summary>
    public static FontLock Read(string path)
    {
        var read = new FontLock();
        if (!File.Exists(path)) return read;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number || version.GetInt32() != Version)
                throw new InvalidDataException($"it is not a version {Version} lock");
            foreach (var provider in root.EnumerateObject().Where(property => property.Name != "version"))
            {
                if (provider.Value.ValueKind != JsonValueKind.Object) throw new InvalidDataException($"'{provider.Name}' is not a set of families");
                foreach (var family in provider.Value.EnumerateObject())
                {
                    if (family.Value.ValueKind != JsonValueKind.Object) throw new InvalidDataException($"'{family.Name}' is not a set of cuts");
                    var cuts = new Dictionary<FontCut, Pin>();
                    foreach (var cut in family.Value.EnumerateObject())
                    {
                        var key = FontCut.Parse(cut.Name) ?? throw new InvalidDataException($"'{cut.Name}' of '{family.Name}' is not a cut");
                        if (cut.Value.ValueKind != JsonValueKind.Object
                            || !cut.Value.TryGetProperty("url", out var url) || url.ValueKind != JsonValueKind.String
                            || !Uri.TryCreate(url.GetString(), UriKind.Absolute, out var uri)
                            || !cut.Value.TryGetProperty("sha256", out var sha) || sha.ValueKind != JsonValueKind.String
                            || sha.GetString() is not { Length: 64 } hash || !hash.All(Uri.IsHexDigit))
                            throw new InvalidDataException($"'{cut.Name}' of '{family.Name}' has no URL and SHA-256 that hold");
                        cuts[key] = new Pin(uri, hash.ToLowerInvariant());
                    }
                    read.Set(provider.Name, family.Name, cuts);
                }
            }
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"it is not JSON: {exception.Message}", exception);
        }
        return read;
    }

    /// <summary>
    /// Writes the lock to <paramref name="path"/> when it says something other than the file there does,
    /// whole: through a file beside it that is renamed over it. A lock that pins nothing is no file.
    /// </summary>
    public void Write(string path)
    {
        if (IsEmpty)
        {
            if (File.Exists(path)) File.Delete(path);
            return;
        }
        var text = Text();
        if (File.Exists(path) && File.ReadAllText(path) == text) return;
        var temporary = path + $".{Environment.ProcessId}.tmp";
        File.WriteAllText(temporary, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>The lock as JSON, families and cuts in order, so a diff shows the pin that moved.</summary>
    public string Text()
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            json.WriteStartObject();
            json.WriteNumber("version", Version);
            foreach (var (provider, families) in _providers.Where(entry => entry.Value.Count > 0))
            {
                json.WriteStartObject(provider);
                foreach (var (family, cuts) in families)
                {
                    json.WriteStartObject(family);
                    foreach (var (cut, pin) in cuts)
                    {
                        json.WriteStartObject(cut.Key);
                        json.WriteString("url", pin.Url.AbsoluteUri);
                        json.WriteString("sha256", pin.Sha256);
                        json.WriteEndObject();
                    }
                    json.WriteEndObject();
                }
                json.WriteEndObject();
            }
            json.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray()).ReplaceLineEndings("\n") + "\n";
    }
}
