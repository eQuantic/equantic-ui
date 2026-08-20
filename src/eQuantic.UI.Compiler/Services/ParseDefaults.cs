using Microsoft.CodeAnalysis.CSharp;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// The ONE set of parse options every eqc tree is created with. Roslyn's default is the latest
/// RELEASED language version, which silently rejects preview syntax (C# 15's labeled jumps,
/// `union`, `closed`, collection-expression `with(...)`) with parse errors long before any
/// strategy could answer for it. eqc parses with <see cref="LanguageVersion.Preview"/>: csc stays
/// the authority on what the APP may use (the project's own LangVersion), and eqc must never be
/// the one that chokes first. Preview is a superset of every released version, so released-code
/// parsing is unchanged.
/// </summary>
public static class ParseDefaults
{
    public static readonly CSharpParseOptions Options =
        CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
}
