using System.Security.Cryptography;

namespace eQuantic.UI.Build;

/// <summary>
/// The families an app declares from Google Fonts, fetched, pinned and laid out in the folder one build
/// embeds them from, one file a cut, named by the family and the cut: <c>Inter-400.ttf</c>,
/// <c>Inter-700-italic.ttf</c>.
/// <list type="bullet">
/// <item>A cut the lock does not pin yet is asked of Google with every cut of its family, and fetched. Its
/// URL and the SHA-256 of its bytes become its pin, in <c>fonts.lock.json</c>.</item>
/// <item>A pinned cut is never asked of Google again: its file is checked against its pin on every build,
/// and fetched from its pinned URL when it is not in the store.</item>
/// <item>Whatever would change the app's text without the app choosing it fails the build, naming the way
/// to renew: a pinned file whose bytes differ, a pinned URL that no longer answers, and a family Google
/// has replaced since its cuts were pinned when a cut is added to it, so one family never mixes two of
/// Google's versions.</item>
/// <item>The lock and the store are the PROJECT's, and what a build declares is its own: the families a
/// build reads depend on its symbols, one per configuration and target framework. So no build takes a pin
/// out of the lock, a family stays pinned until the app takes it out, and each build lays its files out in
/// a folder of its own. The builds of one project take turns over the store and the lock, which those of
/// a Photon app's target frameworks may reach at once.</item>
/// <item>A build that fails changes neither the lock nor its folder: what it fetched waits in the store,
/// embedded nowhere.</item>
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

    /// <summary>How long a build waits for another build of the project to finish with the store and the lock.</summary>
    private static readonly TimeSpan GateTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Lays out in <paramref name="directory"/> the files of <paramref name="declarations"/>, kept in
    /// <paramref name="store"/> by the hash of their bytes, and adds their pins to the lock at
    /// <paramref name="lockPath"/>, asking <paramref name="google"/> only for what is not pinned or not
    /// there. Returns the errors that stop the build, each one line in MSBuild's own form.
    /// </summary>
    public static async Task<IReadOnlyList<string>> FetchAsync(IReadOnlyList<GoogleFontDeclarations.Declaration> declarations,
        string lockPath, string store, string directory, GoogleFonts google)
    {
        Directory.CreateDirectory(store);
        await using var gate = await GateAsync(store);

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

        var errors = new List<string>();
        var laidOut = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var declaration in declarations)
        {
            var pins = pinned.Of(Provider, declaration.Family);
            if (await FetchAsync(declaration, pins, store, google, errors) is not { } family) continue;
            // The cuts this build does not declare keep their pins: another build may.
            pinned.Set(Provider, declaration.Family, pins.Concat(family).GroupBy(entry => entry.Key)
                .ToDictionary(group => group.Key, group => group.Last().Value));
            foreach (var (cut, pin) in family) laidOut[FileName(declaration.Family, cut)] = Stored(store, pin.Sha256);
        }
        if (errors.Count > 0) return errors;

        pinned.Write(lockPath);
        LayOut(directory, laidOut);
        return errors;
    }

    /// <summary>The name a cut's file is laid out and embedded under.</summary>
    public static string FileName(string family, FontCut cut) => $"{Stem(family)}-{cut.Weight}{(cut.Italic ? "-italic" : "")}.ttf";

    /// <summary>What a family's files are named after: its ASCII letters and digits.</summary>
    public static string Stem(string family) => new(family.Where(char.IsAsciiLetterOrDigit).ToArray());

    /// <summary>One family's pins once their files are in <paramref name="store"/>, or null once its errors are added.</summary>
    private static async Task<IReadOnlyDictionary<FontCut, FontLock.Pin>?> FetchAsync(GoogleFontDeclarations.Declaration declaration,
        IReadOnlyDictionary<FontCut, FontLock.Pin> pins, string store, GoogleFonts google, List<string> errors)
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
                var hash = Sha256(bytes);
                WriteWhole(Stored(store, hash), bytes);
                family[cut] = new FontLock.Pin(url, hash);
            }
        }

        foreach (var cut in declaration.Cuts.Where(pins.ContainsKey))
        {
            var pin = pins[cut];
            var stored = Stored(store, pin.Sha256);
            if (File.Exists(stored) && Sha256(File.ReadAllBytes(stored)) == pin.Sha256)
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
            WriteWhole(stored, bytes);
            family[cut] = pin;
        }
        return family;
    }

    /// <summary>
    /// Brings <paramref name="directory"/>, the folder one build embeds from, to the files it lays out, each
    /// copied from the store, and nothing else. A file already there with the same bytes is left alone, so
    /// the compile it feeds does not run again for a file that did not change.
    /// </summary>
    private static void LayOut(string directory, IReadOnlyDictionary<string, string> files)
    {
        Directory.CreateDirectory(directory);
        foreach (var file in Directory.EnumerateFiles(directory).Where(file => !files.ContainsKey(Path.GetFileName(file))))
            File.Delete(file);
        foreach (var (name, stored) in files)
        {
            var path = Path.Combine(directory, name);
            var bytes = File.ReadAllBytes(stored);
            if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)) continue;
            WriteWhole(path, bytes);
        }
    }

    /// <summary>Where the store keeps the file whose bytes hash to <paramref name="sha256"/>.</summary>
    private static string Stored(string store, string sha256) => Path.Combine(store, $"{sha256}.ttf");

    /// <summary>
    /// The project's turn at the store and the lock: a file held open with no sharing, which the builds of
    /// the project's other configurations and target frameworks wait for, rather than one of them
    /// writing a lock that leaves out the pins another just added.
    /// </summary>
    private static async Task<FileStream> GateAsync(string store)
    {
        var path = Path.Combine(store, ".gate");
        var waited = TimeSpan.Zero;
        var pause = TimeSpan.FromMilliseconds(100);
        while (true)
        {
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (waited < GateTimeout)
            {
                await Task.Delay(pause);
                waited += pause;
            }
        }
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

    /// <summary>Writes <paramref name="bytes"/> to <paramref name="path"/> whole: through a file beside it, renamed over it.</summary>
    private static void WriteWhole(string path, byte[] bytes)
    {
        var temporary = Path.Combine(Path.GetDirectoryName(path)!, $".{Path.GetFileName(path)}.{Environment.ProcessId}.tmp");
        File.WriteAllBytes(temporary, bytes);
        File.Move(temporary, path, overwrite: true);
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static string Error(GoogleFontDeclarations.Declaration declaration, string code, string message) =>
        $"{declaration.Place}: error {code}: {message}.";

    private static string Renew(GoogleFontDeclarations.Declaration declaration, string message) =>
        Error(declaration, PinCode, $"{message}. Take '{declaration.Family}' out of {FontLock.FileName}, and the next build "
            + "pins the files Google serves now");
}
