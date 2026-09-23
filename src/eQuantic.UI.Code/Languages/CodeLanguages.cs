namespace eQuantic.UI.Code;

/// <summary>
/// The languages this SDK ships, by name. A registry rather than an enum because an app may bring
/// its own: <c>CodeLanguages.Register(new SqlLanguage())</c> and the editor colours it with no
/// change here.
/// </summary>
public static class CodeLanguages
{
    public static readonly ICodeLanguage CSharp = new CSharpLanguage();
    public static readonly ICodeLanguage TypeScript = new TypeScriptLanguage();
    public static readonly ICodeLanguage Python = new PythonLanguage();
    public static readonly ICodeLanguage Json = new JsonLanguage();
    public static readonly ICodeLanguage Xml = new XmlLanguage();
    public static readonly ICodeLanguage PlainText = new PlainTextLanguage();

    private static readonly Dictionary<string, ICodeLanguage> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["c#"] = CSharp, ["csharp"] = CSharp, ["cs"] = CSharp,
        ["typescript"] = TypeScript, ["ts"] = TypeScript,
        ["javascript"] = TypeScript, ["js"] = TypeScript, ["tsx"] = TypeScript, ["jsx"] = TypeScript,
        ["python"] = Python, ["py"] = Python,
        ["json"] = Json,
        ["xml"] = Xml, ["csproj"] = Xml, ["html"] = Xml, ["plist"] = Xml,
        ["text"] = PlainText, ["txt"] = PlainText, ["plain"] = PlainText,
    };

    /// <summary>Adds (or replaces) a language under a name — an app's own dialect joins here.</summary>
    public static void Register(string name, ICodeLanguage language) => Known[name] = language;

    /// <summary>The language for a name or a file extension; plain text when nothing matches, which
    /// is the answer that renders rather than the one that throws.</summary>
    public static ICodeLanguage For(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return PlainText;
        var key = name.TrimStart('.');
        return Known.TryGetValue(key, out var language) ? language : PlainText;
    }
}
