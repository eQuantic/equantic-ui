using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Extensions;

/// <summary>
/// Recognising an attribute by NAME when the model cannot be asked — the fallback the parser uses in
/// standalone CompileSource, where nothing resolves and the text is all there is.
/// </summary>
public static class AttributeSyntaxExtensions
{
    /// <summary>
    /// True when this attribute is <paramref name="simpleName"/> in any of the spellings C# allows:
    /// bare (<c>[ServerOnly]</c>), with the suffix (<c>[ServerOnlyAttribute]</c>), or qualified to any
    /// depth (<c>[eQuantic.UI.Primitives.ServerOnly]</c>, <c>[global::…ServerOnlyAttribute]</c>).
    /// <para>
    /// Three call sites compared <c>Name.ToString()</c> for exact equality, and each of them missed
    /// the qualified form — an author who writes the namespace out, which is common exactly where
    /// two namespaces both offer the name, got a module emitted for a type they had marked
    /// server-only. One rule, one place: the LAST segment decides, with or without <c>Attribute</c>.
    /// </para>
    /// </summary>
    public static bool IsNamed(this AttributeSyntax attribute, string simpleName)
    {
        var name = attribute.Name switch
        {
            QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
            AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.Text,
            SimpleNameSyntax simple => simple.Identifier.Text,
            _ => attribute.Name.ToString(),
        };
        return name == simpleName || name == simpleName + "Attribute";
    }
}
