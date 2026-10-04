using eQuantic.UI.Codegen;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// The one writer of a v3 source map: the map eqc writes beside each module, and the one it composes
/// with the bundler's (<see cref="SourceMapComposer"/>). Through the build's JSON writer, whose
/// escape is System.Text.Json's, so a C# file holding a control that JSON refuses raw (a form feed is
/// C# whitespace) still makes a map a debugger can read (#525). The two maps were written apart, one
/// by hand, which escaped five characters and wrote the rest raw.
/// </summary>
internal static class SourceMapWriter
{
    /// <param name="file">The generated file the map is for, when it names one.</param>
    /// <param name="sourceRoot">What a source's name is resolved under, when the map has a root.</param>
    /// <param name="sources">Each source's name, in the order the mappings number them.</param>
    /// <param name="contents">Each source's text, or null where the map does not carry it. Written
    /// only when the map carries at least one.</param>
    /// <param name="names">The names segments refer to.</param>
    /// <param name="mappings">The mappings, Base64 VLQ.</param>
    /// <param name="debugId">The id an error reporter matches the map to its module by, when the
    /// bundler gave one.</param>
    public static string Write(string? file, string? sourceRoot, IReadOnlyList<string> sources,
        IReadOnlyList<string?> contents, IReadOnlyList<string> names, string mappings, string? debugId) =>
        JsonWriter.Document(map =>
        {
            map.Number("version", 3);
            if (file is not null) map.String("file", file);
            if (sourceRoot is not null) map.String("sourceRoot", sourceRoot);
            map.Strings("sources", sources);
            if (contents.Any(content => content is not null)) map.Strings("sourcesContent", contents);
            map.Strings("names", names);
            map.String("mappings", mappings);
            if (debugId is not null) map.String("debugId", debugId);
        });
}
