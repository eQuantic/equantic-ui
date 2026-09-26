using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// Composes a module's source map with the maps its sources carry, so the map leads from the
/// JavaScript the browser runs to the C# it was written in: bun maps the JavaScript to the
/// TypeScript eqc wrote, and eqc's own map leads from that TypeScript to C#.
/// <para>
/// This was a script over <c>@ampproject/remapping</c>, which bun's auto-install fetched from npm on
/// a developer's first Debug build (#356). The composition is the same one, rule for rule: every
/// segment of the outer map is traced through the inner map of its source, to the last position on
/// that line at or before its column; a segment that traces to nothing is dropped, one that traces
/// to a position with no source becomes a position with no source, and a segment that repeats the
/// one before it adds nothing. A source that carries no map stays as the outer map names it. The one
/// difference is what the script lost: bun's <c>debugId</c>, which an error reporter matches a map
/// by, is kept.
/// </para>
/// </summary>
public static class SourceMapComposer
{
    private static readonly JsonSerializerOptions Writing = new()
    {
        // A map is read by a debugger and never embedded in markup, and its sources carry the C#:
        // the default escaping would turn every '<' of a generic argument into a six-character escape.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// <paramref name="outerMap"/> composed with the maps its sources carry. <paramref name="innerMapOf"/>
    /// is handed each source by the path the outer map names it with (its sourceRoot joined) and
    /// answers that source's map, or null when it carries none.
    /// </summary>
    public static string Compose(string outerMap, Func<string, string?> innerMapOf)
    {
        var outer = ParsedMap.From(outerMap);
        var inners = outer.Sources
            .Select(source => innerMapOf(source) is { } json ? ParsedMap.From(json, source) : null)
            .ToArray();

        var composed = new ComposedMap();
        foreach (var line in outer.Lines)
        {
            var into = composed.NextLine();
            foreach (var segment in line)
            {
                if (segment.Length == 1)
                {
                    composed.AddSourceless(into, segment[0]);
                    continue;
                }

                var name = segment.Length == 5 ? outer.Names[segment[4]] : null;
                if (inners[segment[1]] is not { } inner)
                {
                    composed.Add(into, segment[0], outer.Sources[segment[1]], outer.ContentOf(segment[1]),
                        segment[2], segment[3], name);
                    continue;
                }

                if (inner.Trace(segment[2], segment[3]) is not { } traced) continue;
                if (traced.Length == 1)
                {
                    composed.AddSourceless(into, segment[0]);
                    continue;
                }
                composed.Add(into, segment[0], inner.Sources[traced[1]], inner.ContentOf(traced[1]),
                    traced[2], traced[3], traced.Length == 5 ? inner.Names[traced[4]] : name);
            }
        }

        return composed.Write(outer.File, outer.DebugId);
    }

    /// <summary>
    /// <paramref name="path"/> with its <c>.</c> and <c>..</c> segments settled, the way a map's
    /// sources resolve: a relative path keeps the <c>..</c> it cannot climb past.
    /// </summary>
    private static string Normalize(string path)
    {
        var rooted = path.StartsWith('/');
        var parts = new List<string>();
        foreach (var part in path.Split('/'))
        {
            if (part is "" or ".") continue;
            if (part == ".." && parts.Count > 0 && parts[^1] != "..") parts.RemoveAt(parts.Count - 1);
            else if (part != ".." || !rooted) parts.Add(part);
        }
        return (rooted ? "/" : "") + string.Join('/', parts);
    }

    /// <summary>A source's name joined to the root it is named under.</summary>
    private static string Join(string root, string source) =>
        root.Length == 0 ? source : root.TrimEnd('/') + "/" + source;

    /// <summary>The directory part of a path, with its trailing slash, or nothing.</summary>
    private static string DirectoryOf(string path) =>
        path.LastIndexOf('/') is var slash and >= 0 ? path[..(slash + 1)] : "";

    /// <summary>
    /// A map as read: its sources RESOLVED (the root joined, and, for an inner map, taken from the
    /// directory of the source that carries it, as a map's sources resolve against the map), its
    /// names, its contents and its mappings decoded.
    /// </summary>
    private sealed class ParsedMap
    {
        public required string[] Sources { get; init; }
        public required string?[] Contents { get; init; }
        public required string[] Names { get; init; }
        public required List<List<int[]>> Lines { get; init; }
        public string? File { get; init; }
        public string? DebugId { get; init; }

        public string? ContentOf(int source) => source < Contents.Length ? Contents[source] : null;

        public static ParsedMap From(string json, string? carriedBy = null)
        {
            var map = JsonNode.Parse(json)?.AsObject()
                ?? throw new FormatException("a source map is a JSON object");
            var root = map["sourceRoot"]?.GetValue<string>() ?? "";
            var from = carriedBy is null ? "" : DirectoryOf(carriedBy);
            return new ParsedMap
            {
                Sources = Strings(map["sources"])
                    .Select(source => Normalize(from + Join(root, source ?? "")))
                    .ToArray(),
                Contents = Strings(map["sourcesContent"]).ToArray(),
                Names = Strings(map["names"]).Select(name => name ?? "").ToArray(),
                Lines = Base64Vlq.DecodeMappings(map["mappings"]?.GetValue<string>() ?? ""),
                File = map["file"]?.GetValue<string>(),
                DebugId = map["debugId"]?.GetValue<string>(),
            };
        }

        private static IEnumerable<string?> Strings(JsonNode? array) =>
            array is JsonArray items ? items.Select(item => item?.GetValue<string>()) : [];

        /// <summary>
        /// The segment a generated position falls in: on its line, the last one starting before its
        /// column, or the FIRST of those starting exactly at it, and null when the line has none that
        /// early. That is trace-mapping's greatest lower bound, which the script traced with.
        /// </summary>
        public int[]? Trace(int line, int column)
        {
            if (line >= Lines.Count) return null;
            var segments = Lines[line];
            int low = 0, high = segments.Count - 1, found = -1;
            while (low <= high)
            {
                var middle = (low + high) >> 1;
                if (segments[middle][0] <= column)
                {
                    found = middle;
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }
            if (found < 0) return null;
            if (segments[found][0] == column)
                while (found > 0 && segments[found - 1][0] == column) found--;
            return segments[found];
        }
    }

    /// <summary>The composed map as it is built: sources and names numbered as they first appear, and
    /// a segment that would repeat the one before it on its line left out.</summary>
    private sealed class ComposedMap
    {
        private readonly List<string> _sources = [];
        private readonly List<string?> _contents = [];
        private readonly Dictionary<string, int> _sourceIndex = new(StringComparer.Ordinal);
        private readonly List<string> _names = [];
        private readonly Dictionary<string, int> _nameIndex = new(StringComparer.Ordinal);
        private readonly List<List<int[]>> _lines = [];

        public List<int[]> NextLine()
        {
            var line = new List<int[]>();
            _lines.Add(line);
            return line;
        }

        /// <summary>A position with no source ends the one before it; at the start of a line, or after
        /// another, it says nothing.</summary>
        public void AddSourceless(List<int[]> line, int column)
        {
            if (line.Count == 0 || line[^1].Length == 1) return;
            line.Add([column]);
        }

        public void Add(List<int[]> line, int column, string source, string? content, int sourceLine,
            int sourceColumn, string? name)
        {
            if (!_sourceIndex.TryGetValue(source, out var sourceAt))
            {
                _sourceIndex[source] = sourceAt = _sources.Count;
                _sources.Add(source);
                _contents.Add(content);
            }
            else if (_contents[sourceAt] is null && content is not null)
            {
                _contents[sourceAt] = content;
            }

            var nameAt = -1;
            if (!string.IsNullOrEmpty(name) && !_nameIndex.TryGetValue(name, out nameAt))
            {
                _nameIndex[name] = nameAt = _names.Count;
                _names.Add(name);
            }

            if (line.Count > 0 && line[^1] is { Length: > 1 } previous
                && previous[1] == sourceAt && previous[2] == sourceLine && previous[3] == sourceColumn
                && (previous.Length == 5 ? previous[4] : -1) == nameAt)
                return;

            line.Add(nameAt < 0
                ? [column, sourceAt, sourceLine, sourceColumn]
                : [column, sourceAt, sourceLine, sourceColumn, nameAt]);
        }

        public string Write(string? file, string? debugId)
        {
            var map = new JsonObject { ["version"] = 3 };
            if (file is not null) map["file"] = file;
            map["sources"] = new JsonArray([.. _sources.Select(source => (JsonNode?)JsonValue.Create(source))]);
            if (_contents.Any(content => content is not null))
                map["sourcesContent"] = new JsonArray([.. _contents.Select(content => (JsonNode?)(content is null ? null : JsonValue.Create(content)))]);
            map["names"] = new JsonArray([.. _names.Select(name => (JsonNode?)JsonValue.Create(name))]);
            map["mappings"] = Base64Vlq.EncodeMappings(_lines);
            if (debugId is not null) map["debugId"] = debugId;
            return map.ToJsonString(Writing);
        }
    }
}
