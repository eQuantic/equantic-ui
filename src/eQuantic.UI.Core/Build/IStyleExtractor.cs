using System.Collections.Generic;

namespace eQuantic.UI.Core.Build;

/// <summary>
/// Extracts style classes from source code.
/// Implemented by each styling framework to provide framework-specific extraction logic.
/// </summary>
public interface IStyleExtractor
{
    /// <summary>
    /// Visits a syntax node to extract style classes.
    /// </summary>
    /// <param name="syntaxNode">The root syntax node to visit (passed as object to avoid Roslyn dependency).</param>
    void Visit(object syntaxNode);
    
    /// <summary>
    /// Returns all extracted style classes.
    /// </summary>
    IEnumerable<string> GetClasses();
}
