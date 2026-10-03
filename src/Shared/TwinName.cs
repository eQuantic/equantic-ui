namespace eQuantic.UI;

/// <summary>
/// The name a transpiled twin gives a C# name: its first character in lower case
/// (<c>FirstName</c> is <c>firstName</c>).
///
/// <para>
/// Two assemblies that cannot reference each other have to answer this identically. eqc names every
/// member it writes into a twin by it, and the hydration manifest the source generator writes names a
/// value the server sends under it, so the browser adopts the value into the member that reads it. While
/// the server guessed this for itself, an auto-property crossed as <c>Downloads</c> into a twin that
/// declares <c>downloads</c>, and never landed. So the rule is one file, linked into both.
/// </para>
/// </summary>
internal static class TwinName
{
    /// <summary>The twin's name for <paramref name="name"/>. Empty or null comes back unchanged.</summary>
    public static string Of(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name.Substring(1);
}
