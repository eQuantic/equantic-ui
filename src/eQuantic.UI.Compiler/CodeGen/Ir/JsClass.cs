using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.CodeGen.Ir;

/// <summary>
/// Where a member (or a class) came from, for the source map: the C# node it stands for, and —
/// for a member with a body the emitter assembled — the node its body starts at,
/// <see cref="BodyLine"/> lines below the signature, past whatever the emitter put in front.
/// </summary>
public sealed record JsOrigin(SyntaxNode? Member, SyntaxNode? Body = null, int BodyLine = 1);

/// <summary>
/// A class: its header pieces and its members, in order. The ONE layout rule for a class body is
/// applied where this is written (see <c>TypeScriptCodeBuilder.Write(JsClass)</c>): a blank line
/// separates a member with a body from whatever precedes it, and a field from a body member before
/// it; fields stay contiguous; nothing trails the last member.
/// </summary>
public sealed record JsClass(
    string Name,
    string? Base,
    IReadOnlyList<string> TypeParameters,
    bool Export,
    bool Abstract,
    IReadOnlyList<JsClassMember> Members,
    JsOrigin? Origin = null);
