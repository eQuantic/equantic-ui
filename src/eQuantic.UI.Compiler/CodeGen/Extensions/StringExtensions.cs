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
    /// strict-mode and FUTURE reserved words, and the literals. A C# author reaches every one of
    /// them: the ones that are not C# keywords as plain names, and the ones that are through the
    /// verbatim escape. `@class`, `@default` and `@this` are ordinary C#, and they arrived as
    /// `class`, `default` and `this`, a module that did not parse, while this list held only the
    /// first kind.
    /// </summary>
    private static readonly HashSet<string> JsReserved = new(StringComparer.Ordinal)
    {
        // Not C# keywords, so reached as they are.
        "package", "implements", "let", "yield", "await", "arguments", "eval", "function", "var",
        "delete", "debugger", "with", "export", "import", "extends", "super", "of",
        // C# keywords, reached through `@`.
        "break", "case", "catch", "class", "const", "continue", "default", "do", "else", "enum",
        "false", "finally", "for", "if", "in", "instanceof", "interface", "new", "null", "private",
        "protected", "public", "return", "static", "switch", "this", "throw", "true", "try",
        "typeof", "void", "while",
    };

    /// <summary>
    /// A C# LOCAL/PARAMETER name as a legal JS identifier: the verbatim escape comes off first
    /// (`@checked` → `checked` — the `@` is C#'s keyword escape and a syntax error in JS), then a
    /// reserved word takes a trailing `$` (`package` → `package$`). Renaming must be applied
    /// at BOTH the declaration and every reference — which is why it lives here, on the one path
    /// both go through. Anything else passes back unchanged, so ordinary names read as authored.
    /// <para>
    /// A `$` because no C# identifier can hold one, so the renamed name cannot land on another
    /// name in its scope. The underscore it took before could: `package` and `package_` in one
    /// scope were both `package_`, and the module did not parse.
    /// </para>
    /// </summary>
    public static string ToJsIdentifier(this string name)
    {
        if (name.StartsWith('@')) name = name[1..];
        return JsReserved.Contains(name) ? name + "$" : name;
    }
}
