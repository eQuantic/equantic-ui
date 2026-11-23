using System.Text.RegularExpressions;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// The catalogue strings each page reads, through every module it reaches (#1782). A page carried the
/// whole catalogue of the app in its HTML, the back office's strings in the shop's pages, and a test could
/// not say a word was absent from one. The server now ships a page what its modules read, from this
/// index, and the browser asks for the rest only if a key it was not handed is read after all.
/// <para>
/// A page reaches what its module imports of its sibling modules, and what those import, the edges the
/// bundler follows (<see cref="AppModules"/>). The factory surface is the exception: <c>AppUI</c> imports
/// every component of the app, so following it would hand every page every string. A module that calls
/// <c>AppUI.statTile(…)</c> reaches the component that factory builds and no other.
/// </para>
/// </summary>
internal static class PageStrings
{
    /// <summary>The module the factory surface's generator writes (<c>AppUI.g.cs</c>).</summary>
    internal const string FactorySurface = "AppUI";

    private static readonly Regex SiblingImport =
        new(@"(?:\bfrom|\bimport)\s*[""']\./(?<module>[^""'/]+)[""']", RegexOptions.Compiled);

    private static readonly Regex FactoryCall = new($@"\b{FactorySurface}\.(?<factory>\w+)\s*\(", RegexOptions.Compiled);

    private static readonly Regex FactoryMember = new(@"\bstatic\s+(?<factory>\w+)\s*\(", RegexOptions.Compiled);

    private static readonly Regex Construction = new(@"\bnew\s+(?<type>\w+)\s*\(", RegexOptions.Compiled);

    /// <summary>
    /// For each of <paramref name="entries"/> (the pages and the layout), the <c>id/key</c> strings read by
    /// the modules it reaches among <paramref name="modules"/>, keyed by its module's name.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyCollection<string>> Of(
        IReadOnlyDictionary<string, CompilationResult> modules, IEnumerable<string> entries)
    {
        IReadOnlyDictionary<string, string> factories = modules.TryGetValue(FactorySurface, out var surface)
            ? FactoriesOf(surface.TypeScript)
            : new Dictionary<string, string>();
        var pages = new SortedDictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (!modules.ContainsKey(entry)) continue;
            var keys = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var reached in Reached(entry, modules, factories))
                keys.UnionWith(modules[reached].StringKeys);
            pages[entry] = keys;
        }
        return pages;
    }

    /// <summary>The modules <paramref name="entry"/> reaches, itself first.</summary>
    internal static IEnumerable<string> Reached(string entry, IReadOnlyDictionary<string, CompilationResult> modules,
        IReadOnlyDictionary<string, string> factories)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal) { entry };
        var pending = new Queue<string>([entry]);
        while (pending.TryDequeue(out var name))
        {
            yield return name;
            var text = modules[name].TypeScript;
            foreach (Match import in SiblingImport.Matches(text))
            {
                var imported = import.Groups["module"].Value;
                // The surface itself reaches nothing on a page's behalf: the calls say what was built.
                if (imported == FactorySurface && name != FactorySurface) continue;
                if (modules.ContainsKey(imported) && seen.Add(imported)) pending.Enqueue(imported);
            }
            foreach (Match call in FactoryCall.Matches(text))
            {
                if (factories.TryGetValue(call.Groups["factory"].Value, out var built)
                    && modules.ContainsKey(built) && seen.Add(built))
                    pending.Enqueue(built);
            }
        }
    }

    /// <summary>Each factory of the surface's module, by its name, with the component it constructs: the
    /// first <c>new</c> after the factory's name and before the next factory's.</summary>
    internal static IReadOnlyDictionary<string, string> FactoriesOf(string surface)
    {
        var factories = new Dictionary<string, string>(StringComparer.Ordinal);
        var members = FactoryMember.Matches(surface).ToList();
        for (var i = 0; i < members.Count; i++)
        {
            var start = members[i].Index + members[i].Length;
            var end = i + 1 < members.Count ? members[i + 1].Index : surface.Length;
            if (Construction.Match(surface, start, end - start) is { Success: true } built)
                factories.TryAdd(members[i].Groups["factory"].Value, built.Groups["type"].Value);
        }
        return factories;
    }
}
