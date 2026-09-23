namespace eQuantic.UI.Code;

/// <summary>C# — the language the SDK itself is written in, and the one every snippet in its own
/// documentation shows.</summary>
public sealed class CSharpLanguage : CurlyBraceLanguage
{
    public override string Name => "C#";

    protected override bool HasVerbatimStrings => true;
    protected override bool HasBracketAttributes => true;

    public override CodeLanguageRules Rules { get; } = new()
    {
        LineComment = "//",
        BlockComment = ("/*", "*/"),
    };

    protected override IReadOnlySet<string> Keywords { get; } = new HashSet<string>
    {
        "abstract", "as", "async", "await", "base", "break", "case", "catch", "checked", "class",
        "const", "continue", "default", "delegate", "do", "else", "enum", "event", "explicit",
        "extern", "file", "finally", "fixed", "for", "foreach", "get", "global", "goto", "if",
        "implicit", "in", "init", "interface", "internal", "is", "lock", "namespace", "new", "not",
        "operator", "out", "override", "params", "partial", "private", "protected", "public",
        "readonly", "record", "ref", "required", "return", "sealed", "set", "sizeof", "stackalloc",
        "static", "struct", "switch", "this", "throw", "try", "typeof", "unchecked", "unsafe",
        "using", "value", "virtual", "volatile", "when", "where", "while", "with", "yield",
    };

    protected override IReadOnlySet<string> TypeWords { get; } = new HashSet<string>
    {
        "bool", "byte", "char", "decimal", "double", "dynamic", "float", "int", "long", "nint",
        "nuint", "object", "sbyte", "short", "string", "uint", "ulong", "ushort", "var", "void",
    };

    protected override IReadOnlySet<string> ConstantWords { get; } = new HashSet<string>
    {
        "true", "false", "null", "default",
    };
}
