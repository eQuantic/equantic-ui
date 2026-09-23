namespace eQuantic.UI.Compiler;

/// <summary>
/// What a transpiled module's source map carries (#352). A map with its sources' content hands the
/// browser the C# it came from, a <c>[ServerAction]</c>'s body included, which is the one thing
/// the server/client split exists to keep on the server. So the content travels only where a
/// developer is debugging.
/// </summary>
public enum SourceMapMode
{
    /// <summary>A map beside each module, linked from it, with the C# inside: Debug.</summary>
    Full,

    /// <summary>A map beside each module, not linked and without the C#: for an error reporter
    /// that uploads maps on its own. Its <c>sources</c> name files the browser never fetches.</summary>
    External,

    /// <summary>No map at all: every configuration but Debug.</summary>
    None,
}
