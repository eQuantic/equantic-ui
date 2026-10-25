namespace eQuantic.UI.Primitives;

/// <summary>
/// A family from Google Fonts that the app ships. The build fetches the file of each weight, and of each
/// weight's italic when <see cref="Italic"/> is set, before the app compiles, embeds them in the app's
/// assembly and writes the family into the app's font catalog, as it does for a face under the app's
/// <c>Assets</c> folder: <c>[assembly: GoogleFont("Inter", FontWeight.Regular, FontWeight.Bold)]</c>, and
/// the theme names <c>Fonts.Inter</c>. The app serves the files itself, and no page asks Google for them.
/// <para>
/// The build pins each file in <c>fonts.lock.json</c> beside the project, by its URL and the hash of its
/// bytes, the first time it fetches it, so the app's text never changes because Google changed a file: a
/// pinned file that changed, or that Google no longer serves, fails the build until its family is taken
/// out of the lock, and the next build pins Google's current files.
/// </para>
/// <para>
/// Read before the app compiles, so it is written out where it stands: the family a string literal and
/// each weight a member of <see cref="FontWeight"/>.
/// </para>
/// <para>
/// HOST ONLY: it marks an assembly, and no page names it.
/// </para>
/// </summary>
/// <param name="family">The family's name at Google Fonts: "Inter", "JetBrains Mono".</param>
/// <param name="weights">The weights to ship, <see cref="FontWeight.Regular"/> when none is named.</param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
[ServerOnly]
public sealed class GoogleFontAttribute(string family, params FontWeight[] weights) : Attribute
{
    /// <summary>The family's name at Google Fonts.</summary>
    public string Family { get; } = family;

    /// <summary>The weights to ship, <see cref="FontWeight.Regular"/> when none was named.</summary>
    public IReadOnlyList<FontWeight> Weights { get; } = weights.Length > 0 ? weights : [FontWeight.Regular];

    /// <summary>Whether each weight's italic ships too.</summary>
    public bool Italic { get; set; }
}
