using System.IO;
using System.Linq;
using eQuantic.UI.Core.Build;
using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Tailwind.Build;

/// <summary>
/// Tailwind CSS style provider.
/// Handles extraction of Tailwind classes and safelist generation.
/// </summary>
public class TailwindStyleProvider : IStyleProvider
{
    public string FrameworkId => "tailwind";
    
    public IStyleExtractor? CreateExtractor(object semanticModel)
    {
        if (semanticModel is SemanticModel sm)
            return new TailwindStyleExtractor(sm);
        return null;
    }
    
    public void OnBuildComplete(StyleBuildContext context)
    {
        // Write tailwind-safelist.txt for Tailwind v4's @source directive
        var safelistPath = Path.Combine(context.IntermediateDirectory, "tailwind-safelist.txt");
        
        Directory.CreateDirectory(context.IntermediateDirectory);
        File.WriteAllLines(safelistPath, context.ExtractedClasses.Distinct().Order());
    }
}
