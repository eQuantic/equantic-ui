namespace eQuantic.UI.Code;

/// <summary>TypeScript, and JavaScript with it — the transpiled twin of everything above.</summary>
public sealed class TypeScriptLanguage : CurlyBraceLanguage
{
    public override string Name => "TypeScript";

    protected override bool HasTemplateStrings => true;
    protected override bool HasAtDecorators => true;

    public override CodeLanguageRules Rules { get; } = new()
    {
        LineComment = "//",
        BlockComment = ("/*", "*/"),
        IndentWidth = 2,
    };

    protected override IReadOnlySet<string> Keywords { get; } = new HashSet<string>
    {
        "abstract", "any", "as", "async", "await", "break", "case", "catch", "class", "const",
        "constructor", "continue", "debugger", "declare", "default", "delete", "do", "else",
        "enum", "export", "extends", "finally", "for", "from", "function", "get", "if",
        "implements", "import", "in", "infer", "instanceof", "interface", "is", "keyof", "let",
        "namespace", "new", "of", "private", "protected", "public", "readonly", "return",
        "satisfies", "set", "static", "super", "switch", "this", "throw", "try", "type", "typeof",
        "var", "while", "yield",
    };

    protected override IReadOnlySet<string> TypeWords { get; } = new HashSet<string>
    {
        "bigint", "boolean", "never", "number", "object", "string", "symbol", "unknown", "void",
    };

    protected override IReadOnlySet<string> ConstantWords { get; } = new HashSet<string>
    {
        "true", "false", "null", "undefined", "NaN", "Infinity",
    };
}
