using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
    /// The lowercase names the EMITTER puts in a member's scope, which C# never declared: the globals
    /// the output reads, <c>console</c> (Console.WriteLine), <c>parseInt</c>, <c>parseFloat</c> and
    /// <c>isNaN</c> (the number parses), <c>crypto</c> (Guid.NewGuid), <c>setTimeout</c> (Task.Delay
    /// and Task.Yield), <c>encodeURI</c>, <c>decodeURI</c>, <c>encodeURIComponent</c> and
    /// <c>decodeURIComponent</c> (Uri) and <c>undefined</c>, and <c>props</c>, the parameter a
    /// constructor takes. A function on one of these breaks code beside it that never named it, so even a name
    /// the casing left alone yields to them. The globals are not trusted to this list staying
    /// complete: <c>LocalFunctionNameTests</c> reads the compiler's own source for every one it
    /// emits, and fails on one missing here.
    /// </summary>
    private static readonly HashSet<string> EmittedNames = new(StringComparer.Ordinal)
    {
        "console", "parseInt", "parseFloat", "isNaN", "crypto", "setTimeout", "encodeURI", "decodeURI",
        "encodeURIComponent", "decodeURIComponent", "undefined", "props",
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

    /// <summary>
    /// The local function <paramref name="name"/> reaches from <paramref name="at"/>, found by the
    /// syntax alone: the innermost block, switch section or file around it that declares one, which
    /// is where C# looks first. For a reference with no model to ask (the playground, a node a
    /// strategy rewrote), so it names the function its declaration named instead of guessing a
    /// member: a call camel-cased by hand there met a declaration this owner had renamed.
    /// </summary>
    public static LocalFunctionStatementSyntax? InScope(SyntaxNode at, string name)
    {
        foreach (var ancestor in at.Ancestors())
        {
            var statements = ancestor switch
            {
                BlockSyntax block => block.Statements,
                SwitchSectionSyntax section => section.Statements,
                CompilationUnitSyntax unit => SyntaxFactory.List(unit.Members.OfType<GlobalStatementSyntax>().Select(global => global.Statement)),
                _ => default(SyntaxList<StatementSyntax>?),
            };
            if (statements?.OfType<LocalFunctionStatementSyntax>()
                    .FirstOrDefault(function => function.Identifier.ValueText == name) is { } found)
                return found;
            // Past the member its body belongs to, no local function is in scope.
            if (ancestor is MemberDeclarationSyntax and not GlobalStatementSyntax) return null;
        }
        return null;
    }

    /// <summary>The name a reference to <paramref name="localFunction"/> reaches.</summary>
    public static string Of(IMethodSymbol localFunction) =>
        // The DEFINITION's syntax: a generic one is referenced constructed (`Id<int>`).
        localFunction.OriginalDefinition.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is LocalFunctionStatementSyntax declaration
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
            // An accessor that takes a value declares `value` without a syntax of its own, and the
            // emitter writes it as the setter's parameter: a `Value()` there was `const value`
            // beside it (Copilot's review of #399).
            else if (node is AccessorDeclarationSyntax accessor
                     && accessor.Keyword.ValueText is "set" or "init" or "add" or "remove")
                taken.Add("value");
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
            taken.Add(names[function] = Candidates(Cased(function.Identifier.ValueText)).First(name => !taken.Contains(name)));

        return names;
    }

    /// <summary>
    /// Whether the function reaches JavaScript under the name it was written with: not when the casing
    /// changed it, not on a name the emitter reads, and never when it starts with an underscore. The
    /// lowerings spell their own bindings that way (<c>_idx</c>, <c>_s</c>, <c>_seq</c>, <c>_sum</c>,
    /// <c>_m</c>…), and casing never produces a leading underscore, so only a name WRITTEN with one
    /// can meet them. Such a name always takes a `$`, which none of them holds, rather than trusting a
    /// list of them to stay complete.
    /// </summary>
    private static bool KeepsItsName(LocalFunctionStatementSyntax function)
    {
        var written = function.Identifier.ValueText;
        return Cased(written) == written && !EmittedNames.Contains(written) && !written.StartsWith('_');
    }

    /// <summary>The spellings a function may take, in order: its cased name, unless it starts with an
    /// underscore, then the name with a `$`, then numbered ones.</summary>
    private static IEnumerable<string> Candidates(string cased)
    {
        if (!cased.StartsWith('_')) yield return cased;
        var stem = cased.TrimEnd('$');
        yield return stem + "$";
        for (var n = 2; ; n++) yield return stem + "$" + n.ToString(CultureInfo.InvariantCulture);
    }

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
