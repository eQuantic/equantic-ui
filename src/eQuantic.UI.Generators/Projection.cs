using System.Collections.Generic;
using System.Linq;

namespace eQuantic.UI.Generators;

/// <summary>
/// What the browser reads of one server value, collected path by path, and spelled as the manifest's
/// projection (<c>HydratedMemberAttribute.Projection</c>).
/// <para>
/// A path is a chain of C# member names joined by <c>.</c>. One whose value the browser reads is
/// written bare, and one the browser only tests for null ends in <c>?</c>. Nothing written at all
/// means only whether the value itself is null.
/// </para>
/// </summary>
internal sealed class Projection
{
    private readonly HashSet<string> _values = new(System.StringComparer.Ordinal);
    private readonly HashSet<string> _presences = new(System.StringComparer.Ordinal);

    /// <summary>The browser reads the value at <paramref name="path"/>, a leaf.</summary>
    public void Value(string path) => _values.Add(path);

    /// <summary>The browser tests whether the value at <paramref name="path"/> is null; the empty path is the value itself.</summary>
    public void Presence(string path) => _presences.Add(path);

    /// <summary>
    /// The manifest's spelling. A path implies that every value on its way is there, so a presence is
    /// written only where nothing deeper is read, and the value itself never: the server writes null
    /// for a null value, and an object otherwise.
    /// </summary>
    public override string ToString()
    {
        var read = _values.ToList();
        foreach (var presence in _presences)
        {
            if (presence.Length == 0) continue;
            var deeper = _values.Concat(_presences)
                .Any(other => other.Length > presence.Length && other.StartsWith(presence + ".", System.StringComparison.Ordinal));
            if (!deeper && !_values.Contains(presence)) read.Add(presence + "?");
        }
        return string.Join(",", read.OrderBy(path => path, System.StringComparer.Ordinal));
    }
}
