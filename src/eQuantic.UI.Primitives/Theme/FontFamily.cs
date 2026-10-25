namespace eQuantic.UI.Primitives;

/// <summary>
/// A family of faces by name, and the files that ship it. What a theme names for its text
/// (<see cref="IAppTheme.TextFamily"/>), its code (<see cref="IAppTheme.MonoFamily"/>) and its roles
/// (<see cref="TypeStyle.Family"/>).
/// <para>
/// A family with no <see cref="Files"/> is one the platform supplies: <c>TextFamily =&gt; new("Georgia")</c>
/// asks for the Georgia the machine has. A family WITH files is one the app ships: the web serves the
/// files and declares them in the theme's stylesheet, and Photon registers them with its text engine
/// the first time text asks for the family. Naming the family is what ships it, so there is no list
/// of faces to keep in step with the names.
/// </para>
/// <para>
/// An app's own families come from its catalog, which the SDK writes from the files under its
/// <c>Assets</c> folder: <c>TextFamily =&gt; Fonts.AtkinsonHyperlegibleNext</c>.
/// </para>
/// </summary>
/// <param name="Name">The family's name, the one its files carry and the one a page's text is set in.</param>
/// <param name="Files">The files that ship it, or none for a family the platform supplies.</param>
public sealed record FontFamily(string Name, params IReadOnlyList<FontFile> Files)
{
    /// <summary>
    /// The same name with the same files. A list compares by reference, and a theme that builds its
    /// families anew on every read would otherwise never equal itself, which every cache keyed on a
    /// style would pay for.
    /// </summary>
    public bool Equals(FontFamily? other) =>
        other is not null && Name == other.Name && Files.SequenceEqual(other.Files);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, Files.Count);

    /// <summary>The family's name.</summary>
    public override string ToString() => Name;
}
