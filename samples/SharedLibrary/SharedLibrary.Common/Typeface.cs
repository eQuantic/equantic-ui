using SharedLibrary.Theme;

namespace SharedLibrary.Common;

/// <summary>
/// The family the solution's apps set their headings in. It reads the Theme project's constant and
/// nothing else of it, so this library's assembly names no reference to that project: a constant is
/// copied into the code that reads it. A page that reads <see cref="Family"/> is the proof that the
/// build took which project references which from the restore graph, and not from the assemblies (#887).
/// </summary>
public static class Typeface
{
    private const string Serif = Fonts.Serif;

    public static string Family => Serif;
}
