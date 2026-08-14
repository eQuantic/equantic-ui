using System.Text.Json.Serialization;

namespace eQuantic.UI.Design;

/// <summary>
/// One request from the editor. Newline-delimited JSON on stdin — the payloads carry whole source
/// buffers, and JSON escapes every newline inside a string, so one message really is one line.
/// </summary>
public sealed record DesignRequest(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("params")] DesignParams? Params);

/// <summary>Everything any method takes, flattened — the protocol is small enough that a union
/// costs more than it saves.</summary>
public sealed record DesignParams
{
    /// <summary>The project directory to build the compilation from (initialize).</summary>
    [JsonPropertyName("projectDir")] public string? ProjectDir { get; init; }

    /// <summary>The MSBuild-resolved reference list, <c>obj/&lt;cfg&gt;/&lt;tfm&gt;/equantic.refs.txt</c>.</summary>
    [JsonPropertyName("refsFile")] public string? RefsFile { get; init; }

    /// <summary>Where the source generators wrote, for the configuration being previewed.</summary>
    [JsonPropertyName("generatedDir")] public string? GeneratedDir { get; init; }

    /// <summary>The file being edited (diagnose/compile).</summary>
    [JsonPropertyName("path")] public string? Path { get; init; }

    /// <summary>The EDITOR'S buffer, which is the point: unsaved text is what the author is looking
    /// at, and a preview built from disk shows them the file they already changed.</summary>
    [JsonPropertyName("text")] public string? Text { get; init; }

    /// <summary>An origin string a rendered element carried back (classify).</summary>
    [JsonPropertyName("origin")] public string? Origin { get; init; }

    /// <summary>The property being set (setProperty).</summary>
    [JsonPropertyName("property")] public string? Property { get; init; }

    /// <summary>Its new value, as C# SOURCE — <c>Variant.Secondary</c>, <c>12</c>, <c>"Save"</c>.
    /// The panel writes C#, because the file does.</summary>
    [JsonPropertyName("value")] public string? Value { get; init; }

    /// <summary>
    /// Every OTHER file the editor currently holds unsaved. A screen is rarely one file — a shell, a
    /// row, a data helper — and a preview that read those from disk would show the last saved version
    /// of everything except the file you happen to be typing in.
    /// </summary>
    [JsonPropertyName("buffers")] public OpenBuffer[]? Buffers { get; init; }
}

/// <summary>One file as the editor currently has it, saved or not.</summary>
public sealed record OpenBuffer(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("text")] string Text);

/// <summary>
/// One thing about a selected node that a panel can show, and one day edit.
/// <para>
/// <c>Editable</c> is about the SYNTACTIC form the call is written in, not about the property. A
/// component's init-only members — <c>Button.Loading</c>, <c>FlexNode.Width</c> — are reachable only
/// through an object initializer, and the factory surface exists precisely so nobody writes one. So
/// the honest answer for those, on a factory call, is "not from here, and here is why" — which beats
/// silently rewriting <c>Button("Save")</c> into <c>new Button("Save") { … }</c> behind the author's
/// back.
/// </para>
/// </summary>
public sealed record NodeProperty(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    /// <summary>argument · initializer · unset.</summary>
    [property: JsonPropertyName("kind")] string Kind,
    /// <summary>The source text of the value as written, when it is written at all.</summary>
    [property: JsonPropertyName("value")] string? Value,
    [property: JsonPropertyName("editable")] bool Editable,
    /// <summary>Why not, when it is not.</summary>
    [property: JsonPropertyName("reason")] string? Reason,
    /// <summary>The closed set a value may take — enum members, so a panel offers a list rather
    /// than a text box.</summary>
    [property: JsonPropertyName("options")] string[]? Options,
    /// <summary>The doc comment's summary, read off the source symbol.</summary>
    [property: JsonPropertyName("summary")] string? Summary);

/// <summary>
/// One text replacement the editor should apply, or the reason there is none.
/// <para>
/// The host computes the edit and the EDITOR applies it. That split is deliberate: an edit that goes
/// through <c>WorkspaceEdit</c> lands in the document's own undo stack, so one Ctrl+Z reverses the
/// whole gesture and an unsaved buffer stays unsaved. A host writing the file itself would fight the
/// editor for the same document and win in the worst way.
/// </para>
/// </summary>
public sealed record EditResult(
    [property: JsonPropertyName("applied")] bool Applied,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("startLine")] int StartLine,
    [property: JsonPropertyName("startColumn")] int StartColumn,
    [property: JsonPropertyName("endLine")] int EndLine,
    [property: JsonPropertyName("endColumn")] int EndColumn,
    [property: JsonPropertyName("newText")] string NewText)
{
    public static EditResult Refused(string reason) => new(false, reason, 0, 0, 0, 0, "");
}

/// <summary>What the inspector shows for one selected node.</summary>
public sealed record InspectResult(
    [property: JsonPropertyName("component")] string Component,
    /// <summary>factory · new · unknown — the form the call is written in, which decides what is
    /// reachable.</summary>
    [property: JsonPropertyName("form")] string Form,
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("properties")] NodeProperty[] Properties);

/// <summary>
/// What may be DONE with a selected node, which is not the same question as where it came from.
/// <para>
/// A row built five hundred times inside a <c>foreach</c> has one source site and no separate
/// existence; a node returned by a helper method is written somewhere else entirely. An editor that
/// offered "delete this row" for either would corrupt a file, so the canvas has to know which it is
/// looking at and say so.
/// </para>
/// </summary>
public sealed record OriginTier(
    /// <summary>literal · derived · foreign.</summary>
    [property: JsonPropertyName("tier")] string Tier,
    /// <summary>One sentence for the reader, naming the construct or the member.</summary>
    [property: JsonPropertyName("reason")] string Reason,
    /// <summary>The member the origin sits in, when it is not the previewed Build.</summary>
    [property: JsonPropertyName("member")] string? Member);

/// <summary>One thing the compiler had to say, anchored to the span that caused it.</summary>
public sealed record DesignMark(
    [property: JsonPropertyName("startLine")] int StartLine,
    [property: JsonPropertyName("startColumn")] int StartColumn,
    [property: JsonPropertyName("endLine")] int EndLine,
    [property: JsonPropertyName("endColumn")] int EndColumn,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("isError")] bool IsError);

/// <summary>What <c>initialize</c> answers: enough for the extension to refuse to show a preview
/// built on a degraded compilation.</summary>
public sealed record InitializeResult(
    [property: JsonPropertyName("assemblyName")] string AssemblyName,
    [property: JsonPropertyName("sourceFiles")] int SourceFiles,
    [property: JsonPropertyName("references")] int References,
    [property: JsonPropertyName("elapsedMs")] int ElapsedMs);

/// <summary>Browser-ready JavaScript and the class to mount, or the marks explaining why there is
/// none.</summary>
public sealed record CompileResult(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("js")] string Js,
    [property: JsonPropertyName("className")] string ClassName,
    [property: JsonPropertyName("marks")] DesignMark[] Marks,
    [property: JsonPropertyName("elapsedMs")] int ElapsedMs);

/// <summary>The answer to one request: a result or an error, never both.</summary>
public sealed record DesignResponse
{
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("result")] public object? Result { get; init; }
    [JsonPropertyName("error")] public string? Error { get; init; }
}
