using System.Collections.Generic;

namespace eQuantic.UI.Core.Build;

/// <summary>
/// Contract for styling framework providers.
/// Each styling library (Tailwind, Bootstrap) implements this interface.
/// </summary>
public interface IStyleProvider
{
    /// <summary>
    /// Framework identifier (e.g. "tailwind", "bootstrap").
    /// </summary>
    string FrameworkId { get; }
    
    /// <summary>
    /// Creates an extractor for the given semantic model.
    /// Returns null if this provider doesn't handle extraction.
    /// </summary>
    /// <param name="semanticModel">The Roslyn SemanticModel (passed as object to avoid Roslyn dependency).</param>
    IStyleExtractor? CreateExtractor(object semanticModel);
    
    /// <summary>
    /// Post-build hook: called after compilation completes.
    /// Use this to generate CSS, write safelists, run build tools, etc.
    /// </summary>
    void OnBuildComplete(StyleBuildContext context);
}

/// <summary>
/// Context provided to style providers during the build complete phase.
/// </summary>
public record StyleBuildContext(
    string OutputDirectory,
    string IntermediateDirectory,
    IReadOnlyList<string> ExtractedClasses
);
