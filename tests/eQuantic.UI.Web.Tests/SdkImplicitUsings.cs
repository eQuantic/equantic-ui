using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The global usings <c>&lt;ImplicitUsings&gt;</c> writes for a project of <c>Microsoft.NET.Sdk</c>,
/// which every shared source is and every app is: all seven, as <c>obj/*.GlobalUsings.g.cs</c> has
/// them. The one file every pipeline here hands eqc beside its sources, so that a name binds in a
/// test as it binds in the build. Each pipeline kept its own copy, and most of them three of the
/// seven: a name from the other four (<c>CancellationTokenSource</c>, <c>Task</c>) bound in the build
/// and not in the test, its twin was written by the path for a type nothing knows, and the test
/// pinned that (#296).
/// </summary>
internal static class SdkImplicitUsings
{
    public const string Text =
        "global using System;\nglobal using System.Collections.Generic;\nglobal using System.IO;\n"
        + "global using System.Linq;\nglobal using System.Net.Http;\nglobal using System.Threading;\n"
        + "global using System.Threading.Tasks;";

    /// <summary>The file, parsed with <paramref name="options"/> (the sources' own, so that one
    /// compilation holds trees of one language version).</summary>
    public static SyntaxTree Tree(CSharpParseOptions? options = null) =>
        CSharpSyntaxTree.ParseText(Text, options, "GlobalUsings.g.cs");
}
