namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// A file a source map leads to: what the map names it, and its text where the map carries it.
/// </summary>
/// <param name="Name">The file's name in the map's <c>sources</c>, resolved under its root.</param>
/// <param name="Content">The file's text, for the map's <c>sourcesContent</c>, or null where the map
/// carries none (<see cref="SourceMapMode.External"/>).</param>
public sealed record SourceMapSource(string Name, string? Content);
