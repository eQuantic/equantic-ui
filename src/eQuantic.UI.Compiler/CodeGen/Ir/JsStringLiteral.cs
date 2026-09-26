namespace eQuantic.UI.Compiler.CodeGen.Ir;

/// <summary>
/// THE SPELLING OF A STRING in the emitted JavaScript: single-quoted, with the backslash, the quote
/// and the two line breaks escaped. A raw line feed or carriage return inside a string literal is a
/// syntax error that takes the whole module with it, and the text was quoted by hand in nine places:
/// five of them, a record's declared defaults among them, escaped the backslash and the quote only,
/// so `string Joined = "a" + "\n"` was written with a line break inside its quotes.
/// </summary>
internal static class JsStringLiteral
{
    public static string Quote(string value) =>
        "'" + value
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r") + "'";
}
