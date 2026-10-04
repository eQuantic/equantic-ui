namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// A projection's reads as a tree, one level per member: what a server value's twin spec is written
/// from (<see cref="TypeScriptEmitter"/>). A read that reaches a leaf records its C# type, which says
/// how the browser coerces it.
/// </summary>
internal sealed class ProjectionReads
{
    private readonly List<(string Segment, ProjectionReads Reads)> _children = new();

    /// <summary>The members read beneath this one, in the order the projection first reads them.</summary>
    public IReadOnlyList<(string Segment, ProjectionReads Reads)> Children => _children;

    /// <summary>The C# type of the value read here, when the projection reads it whole.</summary>
    public Microsoft.CodeAnalysis.ITypeSymbol? Leaf { get; set; }

    public ProjectionReads Child(string segment)
    {
        foreach (var (name, reads) in _children)
            if (name == segment) return reads;
        var child = new ProjectionReads();
        _children.Add((segment, child));
        return child;
    }
}
