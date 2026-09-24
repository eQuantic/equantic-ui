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

    /// <summary>
    /// The same text, and into <paramref name="marks"/> where each statement of the member's body
    /// that carries an origin begins, counted from the start of the member's text (#293). A member
    /// with no body marks nothing.
    /// </summary>
    public static string WriteMarked(JsClassMember member, JsLayout layout, List<JsLineMark> marks)
    {
        var (head, body) = member switch
        {
            JsAccessorMember accessor =>
                ($"{accessor.Modifiers}{accessor.Kind} {accessor.Name}({accessor.Parameters}){accessor.Annotation} ", accessor.Body),
            JsMethodMember method =>
                ($"{method.Modifiers}{method.Name}{method.TypeParameters}({method.Parameters}){method.Annotation} ", method.Body),
            JsConstructorMember ctor => ($"constructor({ctor.Parameters}) ", ctor.Body),
            _ => ((string?)null, (JsStatement?)null),
        };
        if (head is null || body is null) return Write(member, layout);
        var bodyMarks = new List<JsLineMark>();
        var text = head + Body(body, layout, bodyMarks);
        // The body opens on the head's own line; a statement there moves along by the head.
        marks.AddRange(bodyMarks.Select(mark => mark.Line == 0 ? mark with { Column = head.Length + mark.Column } : mark));
        return text;
    }

    /// <summary>A member body between its braces. A block lays itself out; anything else — a raw
    /// statement the emitter still assembles as text — is placed one level in, line by line.</summary>
    private static string Body(JsStatement body, JsLayout layout) => Body(body, layout, new List<JsLineMark>());

    private static string Body(JsStatement body, JsLayout layout, List<JsLineMark> marks)
    {
        if (body is JsBlock block) return JsStatementWriter.WriteMarked(block, layout, 0, marks);
        var contentMarks = new List<JsLineMark>();
        var contents = JsStatementWriter.WriteMarked(body, layout, 0, contentMarks);
        // Braced trims what leads the contents and opens a line and a level before them: a mark
        // moves down past the brace, in by the level, and up past any line the trim took.
        var leading = contents[..(contents.Length - contents.TrimStart().Length)];
        var linesTrimmed = leading.Count(c => c == '\n');
        var columnsTrimmed = leading.Length - leading.LastIndexOf('\n') - 1;
        foreach (var mark in contentMarks)
        {
            var line = mark.Line - linesTrimmed;
            var column = mark.Line == linesTrimmed ? mark.Column - columnsTrimmed : mark.Column;
            marks.Add(mark with { Line = line + 1, Column = column + 4 });
        }
        return Braced(contents);
    }

    /// <summary>Contents between braces, one level in: empty stays <c>{}</c>.</summary>
    public static string Braced(string contents)
    {
        contents = contents.Trim();
        if (contents.Length == 0) return "{}";
        var lines = contents.Split('\n').Select(line => line.Length == 0 ? line : "    " + line);
        return "{\n" + string.Join("\n", lines) + "\n}";
    }
}
