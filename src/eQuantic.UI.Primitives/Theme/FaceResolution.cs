namespace eQuantic.UI.Primitives;

/// <summary>
/// Which named faces this run ASKED for and did not get. Empty is the answer a healthy run gives.
///
/// <para>
/// A text engine never refuses an unknown family — CoreText, DirectWrite and Android all substitute
/// silently, which is right for them and wrong for us: a design handoff draws every metric against a
/// specific face, and rendering the system's instead is a different design that nothing reports.
/// The pixels are the only witness, and they say "that looks a bit off" rather than "IBM Plex Sans
/// is not on this machine".
/// </para>
///
/// <para>
/// Same shape as <see cref="ComponentBoundary.Contained"/>, and for the same reason: a run's hosts
/// print it on the line everyone reads. Names rather than a count — a face that is missing is
/// missing on every glyph of every frame, so a count reports the workload and buries the one fact
/// worth having, which is WHICH family.
/// </para>
/// </summary>
public static class FaceResolution
{
    private static readonly AsyncLocal<List<string>?> _unresolved = new();

    /// <summary>The families this run asked for and the platform could not supply, first seen first.</summary>
    public static IReadOnlyCollection<string> Unresolved =>
        _unresolved.Value is { } seen ? seen.ToArray() : [];

    /// <summary>
    /// Records a family the platform did not supply. Called by a text service AFTER it has asked and
    /// been given something else — asking first and checking a list second would be two answers to
    /// one question, and the substitution is what actually happened.
    /// </summary>
    public static void Missing(string family)
    {
        if (string.IsNullOrEmpty(family)) return;
        var seen = _unresolved.Value ??= [];
        if (!seen.Contains(family)) seen.Add(family);
    }

    /// <summary>Forgets what a run failed to resolve — armed by the host at the start of a run, as
    /// the contained tally is, so a later run is not read as carrying an earlier one's.</summary>
    public static void Clear() => _unresolved.Value = null;
}
