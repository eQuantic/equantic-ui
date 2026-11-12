namespace eQuantic.UI;

/// <summary>
/// The names a module cannot give a binding of its own: the words JavaScript reserves, and the
/// globals the code eqc writes reads. A C# name that is one of them takes a trailing <c>$</c> wherever
/// eqc writes it, a local's (<c>StringExtensions.ToJsIdentifier</c>) and a type's twin's
/// (<see cref="TwinName.OfNested"/>) alike: a <c>$</c> because no C# identifier can hold one, so the
/// name it gives can never meet another name of the scope.
///
/// <para>
/// Two assemblies that cannot reference each other have to answer this identically, as they answer
/// <see cref="TwinName"/>: eqc names a type's twin and its module by it, and the server names the module
/// of the page it serves by it. So the lists are one file, linked into both.
/// </para>
/// </summary>
internal static class JsNames
{
    /// <summary>
    /// Identifiers JS refuses in a module (modules are always strict): the reserved words, the
    /// strict-mode and FUTURE reserved words, and the literals. A C# author reaches every one of
    /// them: the ones that are not C# keywords as plain names, and the ones that are through the
    /// verbatim escape. `@class`, `@default` and `@this` are ordinary C#, and they arrived as
    /// `class`, `default` and `this`, a module that did not parse, while this list held only the
    /// first kind.
    /// </summary>
    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
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
    /// The globals the EMITTED code reads, which C# never declared: <c>Math</c>, <c>Number</c> and
    /// <c>String</c> (the numeric and text lowerings), <c>console</c> (Console.WriteLine),
    /// <c>parseInt</c> and <c>parseFloat</c> (the number parses), <c>crypto</c> (Guid.NewGuid),
    /// <c>undefined</c> (an argument a named one skips), and the rest a lowering calls. A C# name that
    /// keeps its spelling hides the global from every lowering in its scope: <c>var crypto = "xy";</c>
    /// beside <c>Guid.NewGuid()</c> threw, and <c>int undefined = 5;</c> filled the parameter a named
    /// argument skipped (#397). A class of the app's named <c>Math</c> was imported under that name, and
    /// <c>System.Math.Max</c> beside it called the class (#890). The list is not trusted to stay
    /// complete: <c>EmittedGlobalsTests</c> reads the compiler's own source for every global it writes,
    /// and fails on one missing here.
    /// </summary>
    private static readonly HashSet<string> Globals = new(StringComparer.Ordinal)
    {
        "console", "parseInt", "parseFloat", "isNaN", "crypto", "setTimeout", "encodeURI", "decodeURI",
        "encodeURIComponent", "decodeURIComponent", "undefined",
        "Array", "BigInt", "Boolean", "Date", "Error", "Infinity", "JSON", "Map", "Math", "NaN", "Number",
        "Object", "Promise", "RegExp", "Set", "String", "Symbol", "TextDecoder", "TextEncoder",
        "Int8Array", "Uint8Array", "Int16Array", "Uint16Array", "Int32Array", "Uint32Array",
        "Float32Array", "Float64Array",
    };

    /// <summary>The globals the emitted code reads (<see cref="Globals"/>).</summary>
    public static IEnumerable<string> EmittedGlobals => Globals;

    /// <summary>
    /// <paramref name="name"/> as a binding of a module may be named: the verbatim escape off first
    /// (<c>@checked</c> is <c>checked</c>, the <c>@</c> being C#'s syntax and never part of a name), then
    /// a reserved word or a global the emitted code reads after a <c>$</c> (<c>package$</c>,
    /// <c>Math$</c>). Any other name comes back as it is.
    /// </summary>
    public static string Untaken(string name)
    {
        if (name.Length > 0 && name[0] == '@') name = name.Substring(1);
        return Reserved.Contains(name) || Globals.Contains(name) ? name + "$" : name;
    }
}
