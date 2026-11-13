using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// The files whose syntax one compile read, as it read them (#1281).
/// <para>
/// A served eqc compiles again only the files a save can have changed. When a save changed bodies
/// alone, no file binds to anything new, and a file's module still changes when its compile READ the
/// syntax of a changed file: a default interface member's body copied into the class that implements
/// it, a struct method's body read to learn whether it writes its value, a base record's declarations.
/// Those reads happen in many places, so they are recorded where every one of them passes
/// (<see cref="SymbolSources"/>) rather than listed, and a guard fails on a read that does not pass there.
/// </para>
/// <para>
/// A record is opened around a unit of work and collects every file read until it is disposed, and
/// every record open around it collects them too: a cache that keeps an answer opens its own around the
/// work that computes it, keeps what it collected beside the answer, and notes it again into whichever
/// compile takes the answer later (<see cref="Note(IEnumerable{string})"/>).
/// </para>
/// </summary>
internal sealed class SourceReads : IDisposable
{
    private static readonly AsyncLocal<SourceReads?> Open = new();

    private readonly SourceReads? _outer;
    private readonly HashSet<string> _files = new(StringComparer.Ordinal);
    private bool _closed;

    private SourceReads(SourceReads? outer) => _outer = outer;

    /// <summary>Opens a record inside whichever is open. Disposed in the order opened, innermost first.</summary>
    internal static SourceReads Begin()
    {
        var reads = new SourceReads(Open.Value);
        Open.Value = reads;
        return reads;
    }

    /// <summary>The files this record collected, by path.</summary>
    internal IReadOnlyCollection<string> Files => _files;

    /// <summary>The file of <paramref name="tree"/> was read, by every record open.</summary>
    internal static void Note(SyntaxTree? tree)
    {
        if (tree?.FilePath is { Length: > 0 } path) Note(path);
    }

    /// <summary>Files read when an answer was computed, noted again where the answer is taken.</summary>
    internal static void Note(IEnumerable<string> files)
    {
        foreach (var file in files) Note(file);
    }

    private static void Note(string file)
    {
        for (var reads = Open.Value; reads is not null; reads = reads._outer) reads._files.Add(file);
    }

    public void Dispose()
    {
        if (_closed) return;
        _closed = true;
        if (!ReferenceEquals(Open.Value, this))
            throw new InvalidOperationException("A record of the files a compile read was closed out of order.");
        Open.Value = _outer;
    }
}
