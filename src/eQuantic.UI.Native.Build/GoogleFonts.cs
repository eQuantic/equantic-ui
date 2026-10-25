using System.Net;
using System.Text.RegularExpressions;

namespace eQuantic.UI.Build;

/// <summary>
/// Google Fonts, asked through the API it documents, <c>fonts.googleapis.com/css2</c>, as a client that is
/// not a browser. Measured, such a client is answered with one TrueType file per weight and style, at a
/// versioned URL (<c>/s/inter/v20/…</c>), a range of weights included, which is the format every target's
/// text engine reads. A family, a weight or an italic Google does not have is answered with 400 and one
/// sentence for all three, so on a 400 this asks again, for the family alone and then for each cut, and
/// says which it is.
/// </summary>
/// <param name="http">The client every request goes through: the network's, or a test's.</param>
public sealed partial class GoogleFonts(HttpClient http)
{
    /// <summary>The API every family is asked of.</summary>
    public static readonly Uri Api = new("https://fonts.googleapis.com/css2");

    /// <summary>What Google answered for a family: each cut's file, or why it has none.</summary>
    /// <param name="Files">The URL of each cut asked for, when Google serves every one.</param>
    /// <param name="Failure">Why it does not, in words, when it does not.</param>
    public sealed record Answer(IReadOnlyDictionary<FontCut, Uri>? Files, string? Failure);

    /// <summary>A client that asks the network, as eqicon does.</summary>
    public static GoogleFonts OverTheNetwork()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("eQuantic.UI (+https://github.com/eQuantic/equantic-ui)");
        return new GoogleFonts(client);
    }

    /// <summary>Where the files of <paramref name="cuts"/> of <paramref name="family"/> are, or why there are none.</summary>
    public async Task<Answer> FilesAsync(string family, IReadOnlyList<FontCut> cuts)
    {
        string css;
        try
        {
            using var response = await http.GetAsync(Query(family, cuts));
            if (response.StatusCode == HttpStatusCode.BadRequest) return new Answer(null, await MissingAsync(family, cuts));
            if (!response.IsSuccessStatusCode)
                return new Answer(null, $"Google Fonts answered {(int)response.StatusCode} {response.ReasonPhrase} when asked for '{family}'");
            css = await response.Content.ReadAsStringAsync();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return new Answer(null, $"Google Fonts could not be reached to ask for '{family}': {exception.Message}");
        }

        var files = new Dictionary<FontCut, Uri>();
        foreach (Match face in FontFace().Matches(css))
        {
            var body = face.Groups["body"].Value;
            if (Style().Match(body) is not { Success: true } style || FaceWeight().Match(body) is not { Success: true } weight
                || Source().Match(body) is not { Success: true } source
                || !Uri.TryCreate(source.Groups["url"].Value.Trim('\'', '"'), UriKind.Absolute, out var url))
                continue;
            files[new FontCut(int.Parse(weight.Groups["weight"].Value), style.Groups["style"].Value == "italic")] = url;
        }
        var absent = cuts.Where(cut => !files.ContainsKey(cut)).ToList();
        return absent.Count == 0
            ? new Answer(cuts.ToDictionary(cut => cut, cut => files[cut]), null)
            : new Answer(null, $"Google Fonts answered for '{family}' with no file of its {string.Join(", ", absent.Select(cut => cut.Described))}");
    }

    /// <summary>
    /// The bytes at <paramref name="url"/>, or null when Google no longer serves it. A failure to reach it
    /// throws <see cref="HttpRequestException"/>, which is not a file that has gone.
    /// </summary>
    public async Task<byte[]?> DownloadAsync(Uri url)
    {
        try
        {
            using var response = await http.GetAsync(url);
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone) return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (TaskCanceledException exception)
        {
            throw new HttpRequestException($"the request for {url} timed out", exception);
        }
    }

    /// <summary>
    /// What Google does not have of <paramref name="family"/>, asked again one way at a time: the family
    /// alone first, then each cut, since its 400 says the same thing of every one.
    /// </summary>
    private async Task<string> MissingAsync(string family, IReadOnlyList<FontCut> cuts)
    {
        try
        {
            if (!await ServesAsync(new Uri($"{Api}?family={Escaped(family)}")))
                return $"Google Fonts has no family named '{family}'";
            var absent = new List<FontCut>();
            foreach (var cut in cuts)
                if (!await ServesAsync(Query(family, [cut])))
                    absent.Add(cut);
            return absent.Count > 0
                ? $"Google Fonts has no {string.Join(", ", absent.Select(cut => cut.Described))} of '{family}'"
                : $"Google Fonts did not serve the cuts asked of '{family}' together, though it serves each alone";
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return $"Google Fonts could not be reached to ask what it has of '{family}': {exception.Message}";
        }
    }

    private async Task<bool> ServesAsync(Uri query)
    {
        using var response = await http.GetAsync(query);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// The query for <paramref name="cuts"/> of <paramref name="family"/>, its tuples in the order the API
    /// requires: <c>family=Inter:ital,wght@0,400;0,700;1,400</c>, or <c>family=Inter:wght@400;700</c> when
    /// no italic is asked for.
    /// </summary>
    public static Uri Query(string family, IReadOnlyList<FontCut> cuts)
    {
        var ordered = cuts.Distinct().Order().ToList();
        var axes = ordered.Any(cut => cut.Italic)
            ? "ital,wght@" + string.Join(";", ordered.Select(cut => $"{(cut.Italic ? 1 : 0)},{cut.Weight}"))
            : "wght@" + string.Join(";", ordered.Select(cut => cut.Weight));
        return new Uri($"{Api}?family={Escaped(family)}:{axes}");
    }

    /// <summary>A family's name in a query, its spaces as the API writes them.</summary>
    private static string Escaped(string family) => Uri.EscapeDataString(family).Replace("%20", "+");

    [GeneratedRegex(@"@font-face\s*\{(?<body>[^}]*)\}")]
    private static partial Regex FontFace();

    [GeneratedRegex(@"font-style:\s*(?<style>normal|italic)")]
    private static partial Regex Style();

    [GeneratedRegex(@"font-weight:\s*(?<weight>\d+)\s*;")]
    private static partial Regex FaceWeight();

    [GeneratedRegex(@"src:\s*url\((?<url>[^)]+)\)")]
    private static partial Regex Source();
}
