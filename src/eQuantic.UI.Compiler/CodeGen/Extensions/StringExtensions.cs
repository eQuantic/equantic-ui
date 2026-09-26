namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// String extensions used across the code generator. Lives in the <c>CodeGen</c> namespace so every
/// strategy (nested under it) sees these without an extra <c>using</c>.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// PascalCase C# identifier → camelCase JS identifier — the member/property casing the runtime
    /// expects (e.g. <c>FirstName</c> → <c>firstName</c>). Empty/null is returned unchanged.
    /// </summary>
    public static string ToCamelCase(this string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];

    /// <summary>
    /// Identifiers JS refuses in a module (modules are always strict): every keyword, the
    /// strict-mode and future reserved words, and the two names strict mode will not bind. The
    /// keywords are here although C# reserves most of them too, because C#'s verbatim escape makes
    /// every one of them a name (<c>@class</c>, <c>@new</c>, <c>@default</c>): the list once left
    /// them out on the reasoning that they could never arrive, and <c>var @class = 5;</c> was
    /// <c>let class = 5;</c>, a SyntaxError that cost the whole module. <c>undefined</c>, <c>NaN</c>
    /// and <c>Infinity</c> are not reserved, but the emitted code compares against them, and a local
    /// of that name would quietly answer for the global.
    /// </summary>
    private static readonly HashSet<string> JsReserved = new(StringComparer.Ordinal)
    {
        "break", "case", "catch", "class", "const", "continue", "debugger", "default", "delete",
        "do", "else", "export", "extends", "false", "finally", "for", "function", "if", "import",
        "in", "instanceof", "new", "null", "return", "super", "switch", "this", "throw", "true",
        "try", "typeof", "var", "void", "while", "with",
        "let", "static", "yield", "implements", "interface", "package", "private", "protected",
        "public", "enum", "await", "arguments", "eval", "of",
        "undefined", "NaN", "Infinity",
    };

    /// <summary>
    /// A C# LOCAL/PARAMETER name as a legal JS identifier: the verbatim escape comes off first
    /// (`@checked` → `checked` — the `@` is C#'s keyword escape and a syntax error in JS), then a
    /// reserved word takes a trailing underscore (`package` → `package_`). Renaming must be applied
    /// at BOTH the declaration and every reference — which is why it lives here, on the one path
    /// both go through. Anything else passes back unchanged, so ordinary names read as authored.
    /// </summary>
    public static string ToJsIdentifier(this string name)
    {
        if (name.StartsWith('@')) name = name[1..];
        return JsReserved.Contains(name) ? name + "_" : name;
    }
}
