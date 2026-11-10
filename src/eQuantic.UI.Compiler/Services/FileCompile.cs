namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// What compiling one file gave a build, kept so that the next build of a served eqc can take it as it
/// is when the save changed nothing the file read (#1281): its results, the files whose syntax its
/// compile read beyond its own, and what it handed the compiler for the string catalogs. Strings and
/// results alone, so no symbol, tree or compilation outlives the build that made it.
/// </summary>
/// <param name="File">The file, by its full path.</param>
/// <param name="Results">Its modules, warnings and errors, in the order the compile gave them.</param>
/// <param name="Read">The files whose syntax its compile read, by path, its own among them.</param>
/// <param name="Uses">The resource strings its code reads, for the culture catalogs.</param>
/// <param name="Discovered">The resource classes its parse discovered, by catalog id, with their Designer.</param>
internal sealed record FileCompile(
    string File,
    IReadOnlyList<CompilationResult> Results,
    IReadOnlyCollection<string> Read,
    IReadOnlyList<ResourceUse> Uses,
    IReadOnlyList<KeyValuePair<string, string>> Discovered)
{
    /// <summary>Whether every module of the file compiled: one that failed is compiled again.</summary>
    internal bool Succeeded => Results.All(result => result.Success);
}
