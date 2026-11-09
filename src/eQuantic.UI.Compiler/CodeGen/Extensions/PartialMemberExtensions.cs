using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Extensions;

/// <summary>
/// The two halves of a partial member are ONE member, and the twin is written from the half that
/// implements it (#1172). C# 13 and 14 let a property, an indexer and an event be <c>partial</c>: a
/// defining half with no body and an implementing half with one, in one type. A partial method already
/// lowered that way, its defining half having no body to write; the defining half of every other partial
/// member reads like a member of its own, an auto-property's <c>{ get; set; }</c>, a field-like event,
/// so each emitter wrote it beside the implementation: a class held <c>level</c> on each instance over
/// the accessors its implementing half wrote, whose setter then never ran (<c>g.Level = 3</c> read back
/// 3 where C# reads 6), a record's constructor ran that setter and its equality counted the state twice,
/// and two indexers of one signature were refused (EQ1007).
/// <para>
/// So every pass that writes or judges a twin's members reads <see cref="TwinMembers"/>, which leaves
/// the defining halves out. The syntax decides, with no model: C# refuses a defining half with a body
/// and an implementing half without one, so a <c>partial</c> property or indexer whose accessors have no
/// body is the defining one, and a <c>partial</c> field-like event is too, an implementing half being an
/// event with accessors.
/// </para>
/// </summary>
internal static class PartialMemberExtensions
{
    /// <summary>Whether <paramref name="member"/> is the defining half of a partial property, indexer or
    /// event, which is the member its implementing half declares.</summary>
    internal static bool DefinesAPartial(this MemberDeclarationSyntax member) =>
        member.Modifiers.Any(SyntaxKind.PartialKeyword) && member switch
        {
            // An expression-bodied one has no accessor list, and an implementing event's accessors have bodies.
            BasePropertyDeclarationSyntax { AccessorList: { } accessors } =>
                accessors.Accessors.All(accessor => accessor.Body is null && accessor.ExpressionBody is null),
            EventFieldDeclarationSyntax => true,
            _ => false,
        };

    /// <summary>The members <paramref name="type"/>'s twin is written from: every member it declares but
    /// the defining half of a partial property, indexer or event.</summary>
    internal static IEnumerable<MemberDeclarationSyntax> TwinMembers(this TypeDeclarationSyntax type) =>
        type.Members.Where(member => !member.DefinesAPartial());
}
