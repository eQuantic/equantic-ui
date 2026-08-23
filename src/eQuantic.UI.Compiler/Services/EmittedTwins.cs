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
/// <para>
/// The FILE key and the TYPE identity are two different things, and conflating them breaks the
/// rule in one direction or the other. The file is keyed by NAME, because that is all a filename
/// carries — key it by namespace too and the original bug walks straight through. The identity is
/// namespace-qualified, because that is what makes two claims one type — compare only the name and
/// a <c>partial</c> class split across six files reads as six types fighting for one twin. Both
/// halves are needed: same name and same identity is ONE type arriving more than once, same name
/// and a different identity is the collision.
/// </para>
/// </summary>
public sealed class EmittedTwins
{
    // Keyed the way a FILESYSTEM is, not the way C# is. `Seat` and `seat` are two types to the
    // language and one file to Windows and to macOS as it ships — so an ordinal key would let the
    // second overwrite the first on the machines most people build on, and pass on Linux. A source
    // tree has to build the same everywhere, so the case-only pair is refused too.
    private readonly Dictionary<string, Twin> _written = new(StringComparer.OrdinalIgnoreCase);

    private readonly record struct Twin(string Name, string Identity, string Source, string TypeScript);

    /// <summary>
    /// Records a twin about to be written and says what the writer should do with it.
    /// </summary>
    /// <param name="name">The type's name, which is the twin's FILENAME and so the key.</param>
    /// <param name="identity">
    /// The namespace-qualified name: what makes two claims the same TYPE rather than two types.
    /// </param>
    /// <remarks>
    /// A repeat is compared by CONTENT as well: a type that reaches the writer twice with the same
    /// bytes is skipped rather than rewritten, because the SOURCE MAP is not identical even when
    /// the module is — it embeds the path and content of the C# it came from, so writing it again
    /// would leave the module mapped to the wrong file and send a debugger to the wrong line.
    /// </remarks>
    public TwinClaim Claim(string name, string identity, string source, string typeScript,
        Func<string, string> describeSource, out string? message)
    {
        message = null;
        if (!_written.TryGetValue(name, out var first))
        {
            _written[name] = new Twin(name, identity, source, typeScript);
            return TwinClaim.Fresh;
        }

        if (string.Equals(first.Identity, identity, StringComparison.Ordinal))
        {
            // ONE type, reaching the writer more than once. Never an error: no file is being lost
            // to a stranger. It is the same declaration seen twice (a generated file collected
            // under two configurations) or one type split across declarations.
            if (first.TypeScript == typeScript) return TwinClaim.Repeat;

            message = $"'{identity}' is declared in more than one place and eqc emits one module "
                + $"per declaration, so the twin holds only what is in "
                + $"'{describeSource(first.Source)}' — the members declared here are not in it. "
                + "Combine them into a single declaration, or keep the members a component uses "
                + "together in one.";
            return TwinClaim.Divided;
        }

        var other = first.Source == source
            ? "another type of the same name in this file"
            : $"the one in '{describeSource(first.Source)}'";
        var named = first.Name == name
            ? $"two types are named '{name}'"
            : $"'{name}' and '{first.Name}' differ only in case, and a filename on Windows and "
              + "on macOS does not";
        message = $"{named} — this one and {other}. Their twins are ONE file, and the second "
            + "would silently replace the first: the code that used one would get the other's "
            + "members. Namespaces do not separate them, because a twin is named for its TYPE. "
            + "Rename one of them.";
        return TwinClaim.Collision;
    }
}

/// <summary>What the writer should do with a twin it is about to write.</summary>
public enum TwinClaim
{
    /// <summary>The name was free: write the module and its map.</summary>
    Fresh,

    /// <summary>The same module, already written. Writing it again would rewrite its map with one
    /// that points at a different C# file — skip it.</summary>
    Repeat,

    /// <summary>
    /// The same TYPE, reaching the writer with a different module: a declaration eqc cannot merge
    /// into the twin already written. The build continues — the first module stands — and the
    /// dropped members are reported, because a twin missing half its type fails in the browser and
    /// nowhere else. The same-file case is an error (EQ2009); across files it is a warning, since
    /// a partial type whose other halves are server-only is ordinary and correct.
    /// </summary>
    Divided,

    /// <summary>A different type wants a name that is taken. The build stops; see the message.</summary>
    Collision,
}
