using System.Text.RegularExpressions;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// The modules the app's own sources compiled to, and which of them the browser reaches (#1779). A type
/// of a referenced project became a module only when a written module imported it (#624); a type of the
/// app's own was a module whatever it was, so a middleware, an endpoint or a configuration reader no page
/// names was read as client code, and failed the build with EQ2004 and EQ2112 at lines nothing in the
/// browser runs, until it was marked <c>[ServerOnly]</c> by hand. A <c>[GeneratedRegex]</c> did the same
/// through the regex generator's own file. Now the app's types follow the referenced projects' rule.
/// <para>
/// The ROOTS are the app's components, its pages and its layout among them, wherever their files sit: a
/// screen a shell composes, or a page a route in <c>Program.cs</c> names, is a module as it always was, and
/// a component that runs only on the server is still marked <c>[ServerOnly]</c>. From them the walk follows
/// each module's imports of its sibling modules (<c>./Name</c>), the same edges the bundler follows, so a
/// class, a record, a static class or a generated type is a module exactly when the browser code names it.
/// What is never reached is server code by inference: its module is not written and nothing it reported
/// is said, since none of it runs in the browser. A component that does name such a type reaches it, and
/// gets its diagnostics where they are.
/// </para>
/// </summary>
internal sealed class AppModules
{
    private static readonly Regex SiblingImport =
        new(@"(?:\bfrom|\bimport)\s*[""']\./(?<module>[^""'/]+)[""']", RegexOptions.Compiled);

    private readonly List<AppModule> _modules = [];

    /// <summary>One compiled type of the app's, as the build writes it.</summary>
    /// <param name="Result">Its module, warnings and errors.</param>
    /// <param name="File">The file it was compiled from.</param>
    /// <param name="IsEntryCandidate">Whether its file is one the build makes an entry point, which a module
    /// reached from the roots still is.</param>
    /// <param name="Identity">What makes two claims of its name one type.</param>
    /// <param name="Describe">Reads a file back for a message.</param>
    internal sealed record AppModule(
        CompilationResult Result, string File, bool IsEntryCandidate, string Identity, Func<string, string> Describe);

    /// <summary>Adds a compiled type, in the order the build compiled it.</summary>
    public void Add(CompilationResult result, string file, bool isEntryCandidate, string identity, Func<string, string> describe) =>
        _modules.Add(new AppModule(result, file, isEntryCandidate, identity, describe));

    /// <summary>
    /// The modules the browser reaches from the roots, in the order they were added, so a clash of two
    /// names is said as it always was. <paramref name="isLayout"/> names the app's layout.
    /// </summary>
    public IReadOnlyList<AppModule> Reached(Func<CompilationResult, bool> isLayout)
    {
        var byName = _modules.ToLookup(module => module.Result.ComponentName, StringComparer.Ordinal);
        var reached = new HashSet<AppModule>(ReferenceEqualityComparer.Instance);
        var pending = new Queue<AppModule>();
        foreach (var module in _modules.Where(module => IsRoot(module, isLayout)))
            if (reached.Add(module)) pending.Enqueue(module);

        while (pending.TryDequeue(out var module))
        {
            foreach (Match import in SiblingImport.Matches(module.Result.TypeScript))
            foreach (var imported in byName[import.Groups["module"].Value])
                if (reached.Add(imported)) pending.Enqueue(imported);
        }

        return _modules.Where(reached.Contains).ToList();
    }

    private static bool IsRoot(AppModule module, Func<CompilationResult, bool> isLayout) =>
        module.Result.IsPage || isLayout(module.Result) || module.Result.IsComponent;
}
