using System.Security.Cryptography;

namespace eQuantic.UI.Build;

/// <summary>
/// The families an app declares from Google Fonts, fetched, pinned and kept in the folder the SDK embeds
/// them from, one file a cut, named by the family and the cut: <c>Inter-400.ttf</c>, <c>Inter-700-italic.ttf</c>.
/// <list type="bullet">
/// <item>A cut the lock does not pin yet is asked of Google with every cut of its family, and fetched. Its
/// URL and the SHA-256 of its bytes become its pin, in <c>fonts.lock.json</c>.</item>
/// <item>A pinned cut is never asked of Google again: its file is checked against its pin on every build,
/// and fetched from its pinned URL when it is not in the folder.</item>
/// <item>Whatever would change the app's text without the app choosing it fails the build, naming the way
/// to renew: a pinned file whose bytes differ, a pinned URL that no longer answers, and a family Google
/// has replaced since its cuts were pinned when a cut is added to it, so one family never mixes two of
/// Google's versions.</item>
/// <item>A family the app no longer declares leaves the lock and the folder, once a build passes. A build
/// that fails leaves both as they were: a family whose name was mistyped for one build keeps its pins.</item>
/// </list>
/// </summary>
public static class GoogleFontFetch
{
    /// <summary>The provider's name in the lock.</summary>
    public const string Provider = "google";

    /// <summary>The code a face Google cannot serve fails with: a family, a weight or an italic it does not have, or Google out of reach.</summary>
    public const string ProviderCode = "EQ4008";

    /// <summary>The code a pin that no longer holds fails with.</summary>
    public const string PinCode = "EQ4009";

    /// <summary>
    /// Brings <paramref name="directory"/> to the files of <paramref name="declarations"/> and the lock at
    /// <paramref name="lockPath"/> to their pins, asking <paramref name="google"/> only for what is not
    /// pinned or not there. Returns the errors that stop the build, each one line in MSBuild's own form.
    /// </summary>
    public static async Task<IReadOnlyList<string>> FetchAsync(IReadOnlyList<GoogleFontDeclarations.Declaration> declarations,
        string lockPath, string directory, GoogleFonts google)
    {
        FontLock pinned;
        try
        {
            pinned = FontLock.Read(lockPath);
        }
        catch (InvalidDataException exception)
        {
            return [$"{lockPath} : error {PinCode}: {FontLock.FileName} does not hold: {exception.Message}. The SDK writes it, "
                + "so delete it, and the next build pins every family it declares again."];
        }

        Directory.CreateDirectory(directory);
        var errors = new List<string>();
        var next = new FontLock();
        var kept = new HashSet<string>(StringComparer.Ordinal);
        foreach (var declaration in declarations)
        {
            if (await FetchAsync(declaration, pinned.Of(Provider, declaration.Family), directory, google, errors) is not { } family)
                continue;
            next.Set(Provider, declaration.Family, family);
            foreach (var cut in family.Keys) kept.Add(FileName(declaration.Family, cut));
        }
        if (errors.Count > 0) return errors;
        foreach (var file in Directory.EnumerateFiles(directory).Where(file => !kept.Contains(Path.GetFileName(file))))
            File.Delete(file);
        next.Write(lockPath);
        return errors;
    }

    /// <summary>The name a cut's file is kept and embedded under.</summary>
    public static string FileName(string family, FontCut cut) =>
        $"{new string(family.Where(char.IsAsciiLetterOrDigit).ToArray())}-{cut.Weight}{(cut.Italic ? "-italic" : "")}.ttf";

    /// <summary>One family's pins once its files are in <paramref name="directory"/>, or null once its errors are added.</summary>
    private static async Task<IReadOnlyDictionary<FontCut, FontLock.Pin>?> FetchAsync(GoogleFontDeclarations.Declaration declaration,
        IReadOnlyDictionary<FontCut, FontLock.Pin> pins, string directory, GoogleFonts google, List<string> errors)
    {
        var family = new Dictionary<FontCut, FontLock.Pin>();
        var unpinned = declaration.Cuts.Where(cut => !pins.ContainsKey(cut)).ToList();
        if (unpinned.Count > 0)
        {
            var answer = await google.FilesAsync(declaration.Family, declaration.Cuts);
            if (answer.Files is null)
            {
                errors.Add(Error(declaration, ProviderCode, answer.Failure!));
                return null;
            }
            var moved = declaration.Cuts.Where(cut => pins.TryGetValue(cut, out var pin) && pin.Url != answer.Files[cut]).ToList();
            if (moved.Count > 0)
            {
                errors.Add(Renew(declaration, $"Google Fonts has replaced '{declaration.Family}' since its cuts were pinned, and one family "
                    + $"never mixes two of Google's versions: its {moved[0].Described} is at {answer.Files[moved[0]]} now, where "
                    + $"{FontLock.FileName} pins {pins[moved[0]].Url}"));
                return null;
            }
            foreach (var cut in unpinned)
            {
                var url = answer.Files[cut];
                var fetched = await DownloadAsync(google, url);
                if (fetched.Bytes is not { } bytes)
                {
                    errors.Add(Error(declaration, ProviderCode, fetched.Unreachable is { } reason
                        ? $"Google Fonts could not be reached to fetch the {cut.Described} of '{declaration.Family}' from {url}: {reason}"
                        : $"Google Fonts lists the {cut.Described} of '{declaration.Family}' at {url}, and does not serve it"));
                    return null;
                }
                // Checked before it is pinned: a pin is a promise that the bytes are a face the catalog
                // reads, and one made of a web font would fail every build after it, far from its cause.
                try
                {
                    OpenTypeFace.Read(bytes);
                }
                catch (FormatException exception)
                {
                    errors.Add(Error(declaration, ProviderCode, $"Google Fonts served the {cut.Described} of '{declaration.Family}' "
                        + $"from {url} as a file that is not a face the SDK can ship: {exception.Message}"));
                    return null;
                }
                WriteWhole(directory, FileName(declaration.Family, cut), bytes);
                family[cut] = new FontLock.Pin(url, Sha256(bytes));
            }
        }

        foreach (var cut in declaration.Cuts.Where(pins.ContainsKey))
        {
            var pin = pins[cut];
            var path = Path.Combine(directory, FileName(declaration.Family, cut));
            if (File.Exists(path) && Sha256(File.ReadAllBytes(path)) == pin.Sha256)
            {
                family[cut] = pin;
                continue;
            }
            var fetched = await DownloadAsync(google, pin.Url);
            if (fetched.Unreachable is { } unreachable)
            {
                errors.Add(Error(declaration, ProviderCode, $"Google Fonts could not be reached to fetch the {cut.Described} of "
                    + $"'{declaration.Family}' that {FontLock.FileName} pins, from {pin.Url}: {unreachable}"));
                return null;
            }
            if (fetched.Bytes is not { } bytes)
            {
                errors.Add(Renew(declaration, $"Google Fonts no longer serves {pin.Url}, which {FontLock.FileName} pins for the "
                    + $"{cut.Described} of '{declaration.Family}'"));
                return null;
            }
            if (Sha256(bytes) is var hash && hash != pin.Sha256)
            {
                errors.Add(Renew(declaration, $"the file Google Fonts serves at {pin.Url} for the {cut.Described} of "
                    + $"'{declaration.Family}' is not the one {FontLock.FileName} pins: its SHA-256 is {hash}, and the pin is {pin.Sha256}"));
                return null;
            }
            WriteWhole(directory, Path.GetFileName(path), bytes);
            family[cut] = pin;
        }
        return family;
    }

    /// <summary>What fetching a file came to: its bytes, nothing when Google no longer serves it, or why Google could not be reached.</summary>
    private readonly record struct Fetched(byte[]? Bytes, string? Unreachable);

    private static async Task<Fetched> DownloadAsync(GoogleFonts google, Uri url)
    {
        try
        {
            return new Fetched(await google.DownloadAsync(url), null);
        }
        catch (HttpRequestException exception)
        {
            return new Fetched(null, exception.Message);
        }
    }

    /// <summary>
    /// Writes <paramref name="bytes"/> as <paramref name="name"/> in <paramref name="directory"/> whole: through
    /// a file in the folder above it, renamed into it. The folder is shared by the builds of every target
    /// framework, which may run at once, and each takes out of it what no declaration wants: a file being
    /// written there would be taken out from under the build writing it.
    /// </summary>
    private static void WriteWhole(string directory, string name, byte[] bytes)
    {
        var temporary = Path.Combine(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(directory))!,
            $".{name}.{Environment.ProcessId}.tmp");
        File.WriteAllBytes(temporary, bytes);
        File.Move(temporary, Path.Combine(directory, name), overwrite: true);
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static string Error(GoogleFontDeclarations.Declaration declaration, string code, string message) =>
        $"{declaration.Place}: error {code}: {message}.";

    private static string Renew(GoogleFontDeclarations.Declaration declaration, string message) =>
        Error(declaration, PinCode, $"{message}. Take '{declaration.Family}' out of {FontLock.FileName}, and the next build "
            + "pins the files Google serves now");
}
