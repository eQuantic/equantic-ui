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

    /// <summary>
    /// Keyed in LOWER CASE, with every name lowered on its way in and on every lookup (<see cref="KeyOf"/>).
    /// A case-insensitive comparer said the same thing in C# and nothing on the web: the transpiled
    /// dictionary is a plain object, which compares keys exactly, so <c>For("CSharp")</c> answered
    /// C# natively and plain text in the browser (the compiler refuses such a comparer now, EQ2007).
    /// </summary>
    private static readonly Dictionary<string, ICodeLanguage> Known = new()
    {
        ["c#"] = CSharp, ["csharp"] = CSharp, ["cs"] = CSharp,
        ["typescript"] = TypeScript, ["ts"] = TypeScript,
        ["javascript"] = TypeScript, ["js"] = TypeScript, ["tsx"] = TypeScript, ["jsx"] = TypeScript,
        ["python"] = Python, ["py"] = Python,
        ["json"] = Json,
        ["xml"] = Xml, ["csproj"] = Xml, ["html"] = Xml, ["plist"] = Xml,
        ["text"] = PlainText, ["txt"] = PlainText, ["plain"] = PlainText,
    };

    /// <summary>Adds (or replaces) a language under a name or an extension — an app's own dialect
    /// joins here, and is found again however the name is cased.</summary>
    public static void Register(string name, ICodeLanguage language) => Known[KeyOf(name)] = language;

    /// <summary>The language for a name or a file extension, in any case; plain text when nothing
    /// matches, which is the answer that renders rather than the one that throws.</summary>
    public static ICodeLanguage For(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return PlainText;
        return Known.TryGetValue(KeyOf(name), out var language) ? language : PlainText;
    }

    /// <summary>A name or an extension as the registry keys it: no leading dot, lower case.</summary>
    private static string KeyOf(string name) => name.TrimStart('.').ToLowerInvariant();
}
