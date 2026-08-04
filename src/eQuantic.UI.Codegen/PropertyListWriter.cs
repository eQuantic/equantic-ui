namespace eQuantic.UI.Codegen;

/// <summary>
/// An Apple property list. Tab-indented and prologue-first because that is how every plist on a Mac
/// is written, and a generated file that does not look like its hand-written neighbours is a file
/// people distrust.
/// </summary>
public sealed class PropertyListWriter : CodeWriter
{
    public PropertyListWriter() : base(indentationType: IndentationType.Tabs)
    {
    }

    /// <summary>The document: prologue, one root dictionary, and whatever the caller puts in it.</summary>
    public static string Document(Action<PropertyListWriter> body)
    {
        var writer = new PropertyListWriter();
        writer.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        writer.AppendLine("<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" "
            + "\"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">");
        writer.AppendLine("<plist version=\"1.0\">");
        using (writer.BeginScope("<dict>", "</dict>")) body(writer);
        writer.AppendLine("</plist>");
        return writer.ToString();
    }

    public PropertyListWriter String(string key, string value)
    {
        Key(key);
        AppendLine($"<string>{Escape(value)}</string>");
        return this;
    }

    /// <summary>A real boolean. `<string>true</string>` is a STRING, and every reader that asks
    /// the plist for a bool gets the wrong answer from it.</summary>
    public PropertyListWriter Bool(string key, bool value)
    {
        Key(key);
        AppendLine(value ? "<true/>" : "<false/>");
        return this;
    }

    /// <summary>A key whose value is a dictionary — the shape that nests.</summary>
    public PropertyListWriter Dictionary(string key, Action<PropertyListWriter> body)
    {
        Key(key);
        using (BeginScope("<dict>", "</dict>")) body(this);
        return this;
    }

    /// <summary>An EMPTY dictionary is a real declaration in a plist, not an omission.</summary>
    public PropertyListWriter EmptyDictionary(string key)
    {
        Key(key);
        AppendLine("<dict/>");
        return this;
    }

    public PropertyListWriter StringArray(string key, params string[] values)
    {
        Key(key);
        using (BeginScope("<array>", "</array>"))
            foreach (var value in values) AppendLine($"<string>{Escape(value)}</string>");
        return this;
    }

    private void Key(string key) => AppendLine($"<key>{Escape(key)}</key>");

    private static string Escape(string value) => value
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
