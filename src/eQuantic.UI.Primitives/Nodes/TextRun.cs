namespace eQuantic.UI.Primitives;

/// <summary>One inline run of a rich <see cref="Text"/> (see <see cref="Text.Spans"/>): its own
/// color and/or mono face, flowing inside the paragraph. Null color = the paragraph's color.</summary>
public sealed record TextRun(string Content, ColorToken? Color = null, bool Mono = false)
{
    /// <summary>Inline emphasis — one bold word inside a sentence, without splitting the Text.</summary>
    public FontWeight? Weight { get; init; }

    /// <summary>
    /// The SLANTED cut for this run — the other half of inline emphasis, and the one markdown's
    /// <c>*single asterisk*</c> means. It composes with <see cref="Weight"/> and with
    /// <see cref="Mono"/>, so <c>**bold *and italic***</c> is one run carrying both.
    /// </summary>
    public bool Italic { get; init; }

    /// <summary>
    /// A SIZE of its own, for a run that is not the size of the prose around it — inline code at
    /// 13.5 inside a 16 paragraph is the case, and a run inherited the paragraph's size with no way
    /// to say otherwise.
    /// <para>
    /// The same escape hatch <see cref="Text.StyleOverride"/> is, for the same reason: the rungs
    /// are the right default and a run that has to sit between two of them has nowhere else to go.
    /// Only the SIZE is taken — the run keeps the paragraph's line box, which is what makes it a
    /// run rather than a line of its own.
    /// </para>
    /// </summary>
    public TypeStyle? StyleOverride { get; init; }

    /// <summary>
    /// Where this run LINKS to — a link inside a sentence, which nothing else here can express.
    /// <para>
    /// A <see cref="Link"/> around a <see cref="Text"/> makes the whole paragraph one link, and a
    /// Row of Texts breaks between RUNS instead of between words: a sentence with three code spans
    /// wraps at the spans. So mixed emphasis has to be one Text with <see cref="Text.Spans"/> — and
    /// without this, a link in the middle of a sentence was inexpressible. For anything that reads
    /// like prose, that is most paragraphs.
    /// </para>
    /// <para>
    /// A link is a TARGET-NEUTRAL idea — a run of text that goes somewhere when you touch it, which
    /// every platform has. What is missing is realization, not meaning: the native realizer reads
    /// neither this, nor <see cref="Text.Spans"/>, nor even <see cref="Link.Destination"/>, so a paragraph
    /// with links draws on Photon as its text, unlinked. Closing that needs per-run hit testing (the
    /// engine has to know each run's rect) and a navigation action a shell can answer.
    /// </para>
    /// <para>
    /// The SHAPE is universal — an Apple <c>NSAttributedString</c> carries a <c>.link</c> attribute on
    /// a range, an Android <c>Spannable</c> carries a <c>URLSpan</c>: an inline link is a RUN with an
    /// attribute everywhere, and cannot be a separate node because a separate node is what breaks
    /// the line.
    /// </para>
    /// <para>
    /// The NAME is not universal, and it is worth being exact about that. Apple says <c>link</c>,
    /// Android says <c>URLSpan</c>, and <c>href</c> is HTML's word. <c>Destination</c> is none of
    /// theirs, and it is used here for internal consistency with <see cref="Link.Destination"/>,
    /// which shipped first — one concept with two names inside one vocabulary would be worse than
    /// one word shared with the node that wraps it. The abstract layer avoids a target's vocabulary
    /// on purpose (<see cref="Pressable"/>, not "Button" or "Clickable"), and this name is the rule,
    /// not an exception to it.
    /// </para>
    /// </summary>
    public string? Destination { get; init; }
}
