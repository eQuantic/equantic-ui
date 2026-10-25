using System.Threading;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// The families with files a page's text names, collected while the server renders it (#1056): the face
/// twin of <see cref="StyleSink"/>, armed by the SSR pipeline around a render. The server declares their
/// faces in the page's head, leaving out the ones the theme's stylesheet declares, so a family built by
/// hand or read from a library's catalog loads on the web as Photon loads it, the first time text asks.
/// <para>
/// Without an ambient sink nothing is collected: a component realized on its own, outside a page, has no
/// head to declare a face in.
/// </para>
/// </summary>
public sealed class FaceSink
{
    private static readonly AsyncLocal<FaceSink?> _ambient = new();

    /// <summary>The render-scoped sink, or null outside a server render.</summary>
    public static FaceSink? Ambient
    {
        get => _ambient.Value;
        set => _ambient.Value = value;
    }

    /// <summary>The id of the style element a page declares the faces in.</summary>
    public const string ElementId = "eq-faces";

    private readonly List<FontFamily> _families = [];
    private readonly HashSet<FontFamily> _seen = [];

    /// <summary>Adds <paramref name="family"/> when it ships files, once however often the page names it.
    /// A family with none is the platform's, and has nothing to declare.</summary>
    public void Add(FontFamily family)
    {
        if (family.Files.Count > 0 && _seen.Add(family)) _families.Add(family);
    }

    /// <summary>The families, in the order the page first named them.</summary>
    public IReadOnlyList<FontFamily> Families => _families;

    /// <summary>Whether the page named none.</summary>
    public bool IsEmpty => _families.Count == 0;
}
