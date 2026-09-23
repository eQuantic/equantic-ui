namespace eQuantic.UI.Primitives;

/// <summary>What a token MEANS, which is what gives it its colour. Deliberately a small, closed
/// set: a design system has one palette for code, and a language that needs a hundred kinds is
/// asking for a different tool.</summary>
public enum CodeTokenKind : byte
{
    /// <summary>Anything with no special meaning — identifiers, whitespace, unknown text.</summary>
    Plain = 0,
    Keyword = 1,
    /// <summary>Type names: declared types, and the identifiers a language capitalizes by convention.</summary>
    Type = 2,
    String = 3,
    Number = 4,
    Comment = 5,
    /// <summary>Arithmetic, comparison, assignment — the symbols that DO something.</summary>
    Operator = 6,
    /// <summary>Brackets, commas, semicolons — the symbols that only separate.</summary>
    Punctuation = 7,
    /// <summary>An identifier being CALLED or declared as a callable.</summary>
    Function = 8,
    /// <summary>A C# attribute, a decorator, an annotation.</summary>
    Attribute = 9,
    /// <summary>A JSON/XML key or attribute name — the left side of a pair.</summary>
    Property = 10,
    /// <summary>Language constants: true/false/null/None/undefined.</summary>
    Constant = 11,
}
