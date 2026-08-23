namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// Which twin has already been written under which NAME. A twin's file is named for its type —
/// <c>BenchSeat.ts</c> — and nothing else, so two types called <c>BenchSeat</c> in different
/// namespaces are one file, and the second silently replaces the first.
/// <para>
/// C# is perfectly happy with them: the namespaces separate the types. The failure arrives in the
/// browser, where the code that constructed one gets the other's members — a component that dies
/// on a field the class does not have, with nothing in the build to suggest why. This makes it a
/// build ERROR instead, which is the whole of the fix: the compiler refuses what it cannot
/// represent rather than picking one.
/// </para>
/// </summary>
public sealed class EmittedTwins
{
    // Keyed the way a FILESYSTEM is, not the way C# is. `Seat` and `seat` are two types to the
    // language and one file to Windows and to macOS as it ships — so an ordinal key would let the
    // second overwrite the first on the machines most people build on, and pass on Linux. A source
    // tree has to build the same everywhere, so the case-only pair is refused too.
    private readonly Dictionary<string, (string Name, string Source, string TypeScript)> _written =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Records a twin about to be written, and answers the error when its name is already taken by
    /// a DIFFERENT twin. Compared by CONTENT rather than by source path: two types of one name in
    /// the same file collide just as surely as two in different files, and a type legitimately
    /// emitted twice writes the same bytes and is not a collision at all.
    /// </summary>
    /// <returns>Null when the name is free (or re-emitting the same twin); the message otherwise.</returns>
    public string? Claim(string name, string source, string typeScript, Func<string, string> describeSource)
    {
        if (_written.TryGetValue(name, out var first))
        {
            if (first.TypeScript == typeScript) return null;
            var other = first.Source == source
                ? "another type of the same name in this file"
                : $"the one in '{describeSource(first.Source)}'";
            var named = first.Name == name
                ? $"two types are named '{name}'"
                : $"'{name}' and '{first.Name}' differ only in case, and a filename on Windows and "
                  + "on macOS does not";
            return $"{named} — this one and {other}. Their twins are ONE file, and the second "
                + "would silently replace the first: the code that used one would get the other's "
                + "members. Namespaces do not separate them, because a twin is named for its TYPE. "
                + "Rename one of them.";
        }

        _written[name] = (name, source, typeScript);
        return null;
    }
}
