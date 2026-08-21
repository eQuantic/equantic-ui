namespace eQuantic.UI.Compiler.CodeGen.Ir;

/// <summary>
/// The single writer of class members. A member's text starts at column zero — the class builder
/// indents every line of it by the class's own level — and a body goes one level in, one
/// statement per line, whether it arrived as a block of IR or as text the emitter still shapes.
/// </summary>
public static class JsMemberWriter
{
    public static string Write(JsClassMember member, JsLayout layout) => member switch
    {
        JsMemberRaw raw => raw.Text,
        JsFieldMember field =>
            $"{field.Modifiers}{field.Name}{field.Annotation}{(field.Initializer is null ? "" : " = " + field.Initializer)};",
        JsAccessorMember accessor =>
            $"{accessor.Modifiers}{accessor.Kind} {accessor.Name}({accessor.Parameters}){accessor.Annotation} {Body(accessor.Body, layout)}",
        JsMethodMember method =>
            $"{method.Modifiers}{method.Name}{method.TypeParameters}({method.Parameters}){method.Annotation} {Body(method.Body, layout)}",
        JsConstructorMember ctor => $"constructor({ctor.Parameters}) {Body(ctor.Body, layout)}",
        _ => throw new InvalidOperationException($"No writer for IR node {member.GetType().Name}."),
    };

    /// <summary>A member body between its braces. A block lays itself out; anything else — a raw
    /// statement the emitter still assembles as text — is placed one level in, line by line.</summary>
    private static string Body(JsStatement body, JsLayout layout) =>
        body is JsBlock block
            ? JsStatementWriter.Write(block, layout, 0)
            : Braced(JsStatementWriter.Write(body, layout, 0));

    /// <summary>Contents between braces, one level in: empty stays <c>{}</c>.</summary>
    public static string Braced(string contents)
    {
        contents = contents.Trim();
        if (contents.Length == 0) return "{}";
        var lines = contents.Split('\n').Select(line => line.Length == 0 ? line : "    " + line);
        return "{\n" + string.Join("\n", lines) + "\n}";
    }
}
