using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// What a C# local function is called in JavaScript: its name camel-cased, as a method's is, made a
/// legal JS identifier, and renamed when the member it lives in already holds that name.
/// <para>
/// The rename exists because camel-casing is a change of name C# never sees. C# is case-sensitive,
/// so a local <c>d</c> and a local function <c>D</c> are two names, and in JavaScript both are
/// <c>d</c>. Measured before this owner existed: beside a local of that name the module did not
/// parse ("d" has already been declared), under a parameter of that name the call reached the
/// number (a TypeError, recursion included), in an inner block the outer local read the function
/// (a wrong answer and no error), and a function called <c>ParseInt</c> called itself where
/// <c>Convert.ToInt32</c> meant the global (a RangeError).
/// </para>
/// <para>
/// The casing itself stays: a function keeping its C# spelling would shadow the PascalCase names a
/// module imports (<c>Row</c>, <c>Text</c>) wherever it is in scope, which is the collision the
/// casing keeps apart.
/// </para>
/// <para>
/// One owner, because the name is written in three places, the <c>const</c> the declaration emits, a
/// method group and a direct call, and the copies had drifted: the direct call camel-cased without
/// the JS-identifier rename, so a local function called <c>Delete</c> was declared as one name and
/// called as <c>delete()</c>, which JavaScript does not parse.
/// </para>
/// </summary>
internal static class LocalFunctionName
{
    /// <summary>
    /// The names the EMITTER puts in a member's scope, which C# never declared. The free names the
    /// output reads: <c>console</c> (Console.WriteLine), <c>parseInt</c> and <c>parseFloat</c>
    /// (Convert.ToInt32 and ToDouble over text), <c>crypto</c> (Guid.NewGuid), <c>encodeURIComponent</c>
    /// and <c>decodeURIComponent</c> (Uri.EscapeDataString and UnescapeDataString) and
    /// <c>undefined</c>. The names it binds: <c>props</c>, the parameter a constructor takes, and the
    /// lowerings' own, <c>_seq</c> (an iterator's buffer), <c>_s</c> (a switch's subject), and
    /// <c>_sum</c>, <c>_x</c>, <c>_a</c>, <c>_b</c> (Sum's and Average's accumulators). A function on
    /// one of these breaks code beside it that never named it, so even a name the casing left alone
    /// yields to them.
    /// </summary>
    private static readonly HashSet<string> EmittedNames = new(StringComparer.Ordinal)
    {
        "console", "parseInt", "parseFloat", "crypto", "encodeURIComponent", "decodeURIComponent",
        "undefined", "props", "_seq", "_s", "_sum", "_x", "_a", "_b",
    };

    /// <summary>
    /// The names every local function of a member takes, assigned once per member. A function of the
    /// syntax alone, so a declaration and its references, converted at different moments, read the
    /// same answer.
    /// </summary>
    private static readonly ConditionalWeakTable<SyntaxNode, IReadOnlyDictionary<LocalFunctionStatementSyntax, string>> Assigned = new();

    /// <summary>
    /// The name <paramref name="declaration"/> declares. Resolved through the symbol when the model
    /// knows the node, so a declaration a strategy rewrote names the function its references name.
    /// </summary>
    public static string Of(LocalFunctionStatementSyntax declaration, ConversionContext context) =>
        context.SemanticHelper.GetDeclaredSymbol(declaration) is IMethodSymbol symbol
            ? Of(symbol)
            : Named(declaration);

    /// <summary>The name a reference to <paramref name="localFunction"/> reaches.</summary>
    public static string Of(IMethodSymbol localFunction) =>
        localFunction.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is LocalFunctionStatementSyntax declaration
            ? Named(declaration)
            : Cased(localFunction.Name);

    private static string Named(LocalFunctionStatementSyntax declaration) =>
        Assigned.GetValue(MemberOf(declaration), Assign).TryGetValue(declaration, out var name)
            ? name
            : Cased(declaration.Identifier.ValueText);

    /// <summary>The name as a C# method's becomes one: camel-cased, then a legal JS identifier.</summary>
    private static string Cased(string name) => name.ToCamelCase().ToJsIdentifier();

    /// <summary>
    /// The member whose body the function lives in. Its declarations are more than the scopes the
    /// function can see, since a sibling block's local counts too: a rename that one causes costs
    /// nothing, where a name missed is the defect.
    /// </summary>
    private static SyntaxNode MemberOf(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            // Top-level statements share one scope, the file's.
            if (ancestor is GlobalStatementSyntax) return ancestor.Parent ?? ancestor;
            if (ancestor is MemberDeclarationSyntax and not BaseTypeDeclarationSyntax and not BaseNamespaceDeclarationSyntax)
                return ancestor;
        }
        return node.SyntaxTree.GetRoot();
    }

    private static IReadOnlyDictionary<LocalFunctionStatementSyntax, string> Assign(SyntaxNode member)
    {
        var taken = new HashSet<string>(EmittedNames, StringComparer.Ordinal);
        var functions = new List<LocalFunctionStatementSyntax>();
        foreach (var node in member.DescendantNodes())
        {
            if (node is LocalFunctionStatementSyntax function) functions.Add(function);
            else if (Declared(node) is { } name) taken.Add(name.ValueText.ToJsIdentifier());
        }

        var names = new Dictionary<LocalFunctionStatementSyntax, string>();

        // A name that reaches JavaScript as it was written keeps it: C# already keeps it apart from
        // every name its scopes declare, and JavaScript scopes it the same way. The emitter's names
        // are the ones C# never saw, so it keeps it only off those.
        foreach (var function in functions.Where(KeepsItsName))
            taken.Add(names[function] = function.Identifier.ValueText);

        // One the casing changed, or one on an emitted name, takes the first spelling nothing holds.
        // A `$` is a character no C# name holds, so the suffix cannot land on one.
        foreach (var function in functions.Where(f => !names.ContainsKey(f)))
        {
            var cased = Cased(function.Identifier.ValueText);
            var name = cased;
            for (var n = 1; taken.Contains(name); n++)
                name = cased.TrimEnd('$') + "$" + (n == 1 ? "" : n.ToString(CultureInfo.InvariantCulture));
            taken.Add(names[function] = name);
        }

        return names;
    }

    private static bool KeepsItsName(LocalFunctionStatementSyntax function) =>
        Cased(function.Identifier.ValueText) == function.Identifier.ValueText
        && !EmittedNames.Contains(function.Identifier.ValueText);

    /// <summary>
    /// The name <paramref name="node"/> binds in JavaScript's scope, where it binds one: a parameter,
    /// a local, a loop or catch variable, a pattern or out designation, a query's range variable.
    /// </summary>
    private static SyntaxToken? Declared(SyntaxNode node) => node switch
    {
        ParameterSyntax parameter => parameter.Identifier,
        VariableDeclaratorSyntax variable => variable.Identifier,
        ForEachStatementSyntax loop => loop.Identifier,
        CatchDeclarationSyntax caught => caught.Identifier,
        SingleVariableDesignationSyntax designation => designation.Identifier,
        FromClauseSyntax from => from.Identifier,
        LetClauseSyntax let => let.Identifier,
        JoinClauseSyntax join => join.Identifier,
        JoinIntoClauseSyntax into => into.Identifier,
        QueryContinuationSyntax continuation => continuation.Identifier,
        _ => null,
    };
}
