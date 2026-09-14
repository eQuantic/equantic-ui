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

    /// <summary>The container a node is being moved INTO, as its own origin (moveAcross).</summary>
    [JsonPropertyName("target")] public string? Target { get; init; }

    /// <summary>How far to move a child: -1 is up, +1 is down (moveChild).</summary>
    [JsonPropertyName("delta")] public int? Delta { get; init; }

    /// <summary>Where in a children list to insert — 0 is before the first (insertChild).</summary>
    [JsonPropertyName("index")] public int? Index { get; init; }

    /// <summary>The frame size a native render is asked for (renderNative).</summary>
    [JsonPropertyName("width")] public int? Width { get; init; }
    [JsonPropertyName("height")] public int? Height { get; init; }

    /// <summary>comfortable · compact — the density the frame is rendered at (renderNative).</summary>
    [JsonPropertyName("density")] public string? Density { get; init; }

    /// <summary>Which list of the call is meant — <c>children</c> unless something else is named.</summary>
    [JsonPropertyName("list")] public string? List { get; init; }

    /// <summary>The C# to insert there, as a palette entry's snippet.</summary>
    [JsonPropertyName("snippet")] public string? Snippet { get; init; }

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
/// <param name="Kind">argument · initializer · unset.</param>
/// <param name="Value">The source text of the value as written, when it is written at all.</param>
/// <param name="Reason">Why not, when it is not.</param>
/// <param name="Options">
/// The closed set a value may take — enum members, so a panel offers a list rather
/// than a text box.
/// </param>
/// <param name="Summary">The doc comment's summary, read off the source symbol.</param>
/// <param name="Members">
/// The members of the VALUE written here, when it is written as a construction — a
/// <c>BoxStyle</c>'s padding, background and radius.
/// <para>
/// Without this a panel can only show the raw C# of the whole initializer in one cell, which is
/// the thing an author most wants to change and the one thing they cannot change there. Each
/// member is an ordinary property in its own right, so the same row, the same editor, the same
/// refusals.
/// </para>
/// </param>
/// <param name="Name">The property's name as C# spells it — <c>Variant</c>, <c>Width</c>.</param>
/// <param name="Type">Its declared type, for a panel that offers the right editor.</param>
/// <param name="Editable">Whether the panel may write this one from here.</param>
public sealed record NodeProperty(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("value")] string? Value,
    [property: JsonPropertyName("editable")] bool Editable,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("options")] string[]? Options,
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("members")] NodeProperty[]? Members = null);

/// <summary>
/// One text replacement the editor should apply, or the reason there is none.
/// <para>
/// The host computes the edit and the EDITOR applies it. That split is deliberate: an edit that goes
/// through <c>WorkspaceEdit</c> lands in the document's own undo stack, so one Ctrl+Z reverses the
/// whole gesture and an unsaved buffer stays unsaved. A host writing the file itself would fight the
/// editor for the same document and win in the worst way.
/// </para>
/// </summary>
/// <param name="Origin">
/// Where the edited node ENDS UP, for the edits that move it — null for the ones that do not.
/// <para>
/// An origin is a span, so moving the text invalidates the one the caller was holding: asking
/// about it afterwards describes whatever slid into those coordinates, which is the sibling it
/// was just swapped with. The panel needs the new one to go on talking about the same node.
/// </para>
/// </param>
/// <param name="Applied">Whether there is an edit at all — false means <see cref="Reason"/> says why not.</param>
/// <param name="Reason">Why the edit was refused, when it was.</param>
/// <param name="StartLine">Where the replaced span begins, zero-based.</param>
/// <param name="StartColumn">The column the replaced span begins at, zero-based.</param>
/// <param name="EndLine">Where the replaced span ends, zero-based.</param>
/// <param name="EndColumn">The column the replaced span ends at, zero-based.</param>
/// <param name="NewText">What to put in that span's place.</param>
public sealed record EditResult(
    [property: JsonPropertyName("applied")] bool Applied,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("startLine")] int StartLine,
    [property: JsonPropertyName("startColumn")] int StartColumn,
    [property: JsonPropertyName("endLine")] int EndLine,
    [property: JsonPropertyName("endColumn")] int EndColumn,
    [property: JsonPropertyName("newText")] string NewText,
    [property: JsonPropertyName("origin")] string? Origin = null)
{
    public static EditResult Refused(string reason) => new(false, reason, 0, 0, 0, 0, "");
}

/// <summary>What the inspector shows for one selected node.</summary>
/// <param name="Form">
/// factory · new · unknown — the form the call is written in, which decides what is
/// reachable.
/// </param>
/// <param name="ChildCount">
/// How many children the declarative list holds, or -1 when there is no such list —
/// which is what decides whether an insert affordance is offered at all.
/// </param>
/// <param name="InsertReason">Why this container cannot take an insertion, when it cannot.</param>
/// <param name="SiblingIndex">
/// This node's position among its parent's declarative children, or -1 when it is not
/// an element of such a list — which is what decides whether it can be moved or removed.
/// </param>
/// <param name="Lists">Every list this call is written with, children included.</param>
/// <param name="InLayer">
/// Whether this node sits in a layered parent, where its position is its depth — which
/// is what makes "move up" mean "send backward".
/// </param>
/// <param name="Component">The component's type name, as the panel's heading.</param>
/// <param name="Summary">Its doc comment's summary, when it has one.</param>
/// <param name="Properties">Every property the panel can show for this node.</param>
/// <param name="SiblingCount">How many children the parent's list holds, which bounds a move.</param>
public sealed record InspectResult(
    [property: JsonPropertyName("component")] string Component,
    [property: JsonPropertyName("form")] string Form,
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("properties")] NodeProperty[] Properties,
    [property: JsonPropertyName("childCount")] int ChildCount,
    [property: JsonPropertyName("insertReason")] string? InsertReason,
    [property: JsonPropertyName("siblingIndex")] int SiblingIndex,
    [property: JsonPropertyName("siblingCount")] int SiblingCount,
    [property: JsonPropertyName("lists")] NodeList[]? Lists = null,
    [property: JsonPropertyName("inLayer")] bool InLayer = false);

/// <summary>
/// One list a call is written with — <c>children</c>, but also a Grid's <c>columns</c>, a menu's
/// <c>items</c>, a dialog's <c>actions</c>. All the same shape, so all the same gestures.
/// </summary>
/// <param name="ElementType">What its elements are, for a palette that offers the right things.</param>
/// <param name="Visual">Whether its elements are nodes on the canvas, or data that only the panel can reach.</param>
/// <param name="Layered">
/// Whether this list's order is PAINT order rather than position — a <c>Stack</c>. Its children
/// overlap, so a caret drawn between two of them marks a place that does not exist, and moving one
/// changes what is in front rather than what is where.
/// </param>
/// <param name="Entries">
/// Each entry as it is written, for the DATA lists only — <c>GridTrack.Flex()</c> reads as itself
/// and a panel can list them. A visual list's elements are whole subtrees, so carrying their text
/// would put a screen's worth of source in the answer to every hover.
/// </param>
/// <param name="Name">The parameter this list is written as — <c>children</c>, <c>columns</c>.</param>
/// <param name="Count">How many entries it holds.</param>
public sealed record NodeList(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("elementType")] string ElementType,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("visual")] bool Visual,
    [property: JsonPropertyName("layered")] bool Layered = false,
    [property: JsonPropertyName("entries")] string[]? Entries = null);

/// <summary>One entry a palette can offer, with the smallest call that compiles.</summary>
/// <param name="Source">
/// app · framework — where the component comes from. The developer's own are what they
/// were just writing, so a picker that buries them among a hundred framework names is a picker
/// they have to search rather than read.
/// </param>
/// <param name="Name">The component's type name, as the palette lists it.</param>
/// <param name="Snippet">The smallest call that compiles, which is what an insert writes.</param>
/// <param name="Summary">Its doc comment's summary, when it has one.</param>
public sealed record PaletteEntry(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("snippet")] string Snippet,
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("source")] string Source = "framework");

/// <summary>
/// What may be DONE with a selected node, which is not the same question as where it came from.
/// <para>
/// A row built five hundred times inside a <c>foreach</c> has one source site and no separate
/// existence; a node returned by a helper method is written somewhere else entirely. An editor that
/// offered "delete this row" for either would corrupt a file, so the canvas has to know which it is
/// looking at and say so.
/// </para>
/// </summary>
/// <param name="Tier">literal · derived · foreign.</param>
/// <param name="Reason">One sentence for the reader, naming the construct or the member.</param>
/// <param name="Member">The member the origin sits in, when it is not the previewed Build.</param>
public sealed record OriginTier(
    [property: JsonPropertyName("tier")] string Tier,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("member")] string? Member);

/// <summary>
/// One Photon frame, or the reason there is none. The pixels come back as a base64 PNG: the protocol
/// is one JSON object per line, and 20 KB of image inside a string is cheaper than a second channel.
/// </summary>
/// <param name="TextService">
/// Whether real text metrics were available — false means placeholder boxes, and the
/// panel says so rather than letting the frame quietly lie about typography.
/// </param>
/// <param name="Nodes">
/// The frame's hit map: every laid-out node that knows the C# that built it, with its
/// absolute bounds, in paint order — so a click on the PICTURE resolves to a file and a span with
/// no process left alive to ask.
/// </param>
/// <param name="Success">Whether there is a frame — false means <see cref="Reason"/> says why not.</param>
/// <param name="Png">The frame as a base64 PNG, when there is one.</param>
/// <param name="Width">The frame's width in pixels.</param>
/// <param name="Height">The frame's height in pixels.</param>
/// <param name="Reason">Why no frame was produced, when none was.</param>
/// <param name="ElapsedMs">How long the render took, for a panel that shows its own cost.</param>
public sealed record NativeFrameResult(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("png")] string? Png,
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height,
    [property: JsonPropertyName("textService")] bool TextService,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("elapsedMs")] int ElapsedMs,
    [property: JsonPropertyName("nodes")] NativeNode[]? Nodes = null)
{
    public static NativeFrameResult Refused(string reason) => new(false, null, 0, 0, false, reason, 0);
}

/// <summary>One node of a rendered Photon frame: where it came from, and where it landed.</summary>
public sealed record NativeNode(
    [property: JsonPropertyName("o")] string Origin,
    [property: JsonPropertyName("n")] string Name,
    [property: JsonPropertyName("x")] float X,
    [property: JsonPropertyName("y")] float Y,
    [property: JsonPropertyName("w")] float Width,
    [property: JsonPropertyName("h")] float Height);

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
