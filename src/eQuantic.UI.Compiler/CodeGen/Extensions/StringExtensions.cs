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
    /// Identifiers JS refuses in a module (modules are always strict): the reserved words, the
    /// strict-mode and FUTURE reserved words, and the names strict mode will not bind. A C# keyword
    /// among them still arrives, through the verbatim escape: `@class` is a legal parameter, and with
    /// the `@` taken off it was declared as `class`, which no module parses (found in #432, where the
    /// list said `class`, `new` and `if` never arrive).
    /// </summary>
    private static readonly HashSet<string> JsReserved = new(StringComparer.Ordinal)
    {
        "package", "interface", "implements", "let", "yield", "enum", "await", "arguments",
        "eval", "function", "var", "typeof", "instanceof", "delete", "debugger", "with",
        "export", "import", "extends", "super", "of",
        // C# keywords too, reached as `@class`, `@new`…
        "break", "case", "catch", "class", "const", "continue", "default", "do", "else", "false",
        "finally", "for", "if", "in", "new", "null", "private", "protected", "public", "return",
        "static", "switch", "this", "throw", "true", "try", "void", "while",
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
