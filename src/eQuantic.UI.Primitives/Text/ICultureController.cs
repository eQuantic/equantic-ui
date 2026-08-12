namespace eQuantic.UI.Primitives;

/// <summary>
/// The app's hand on the LANGUAGE — a SERVICE, not a node, exactly like
/// <see cref="IThemeController"/>: the culture belongs to the HOST (a request negotiates it on the
/// web, the platform reports it in a window), and a component that offers a switcher asks for this
/// the way it asks for a camera, never learning which target answered.
/// <para>
/// A PAIR, because .NET's is: <c>CurrentUICulture</c> picks RESOURCES and <c>CurrentCulture</c>
/// picks FORMATS, and an app may legitimately run pt-BR strings over en-US numbers. Collapsing
/// them would be a quiet departure from the experience this track exists to reproduce (D13).
/// </para>
/// <para>
/// BCP-47 NAMES, never <c>CultureInfo</c>: this contract crosses to the browser through eqc, where
/// no such type exists, and a name is the one currency .NET, the web and both mobile platforms
/// already share. Each realization materializes what it needs — the native controller builds the
/// <c>CultureInfo</c> pair and writes the process statics, the web controller swaps the string
/// catalog, re-renders without a reload (D6) and lets the server know for the next request.
/// </para>
/// </summary>
public interface ICultureController
{
    /// <summary>The UI culture in force — the name resources resolve against.</summary>
    string UICulture { get; }

    /// <summary>The format culture in force — the name numbers and dates resolve against.</summary>
    string FormatCulture { get; }

    /// <summary>
    /// Applies the pair to the running target. An empty or null <paramref name="formatCulture"/>
    /// means "the UI culture's own formats", which is the common case and the only thing a
    /// single-locale platform can report anyway.
    /// </summary>
    void Apply(string uiCulture, string? formatCulture = null);
}
