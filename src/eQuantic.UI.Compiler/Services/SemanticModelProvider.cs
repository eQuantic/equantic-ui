using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;

namespace eQuantic.UI.Compiler.Services;

public class SemanticModelProvider
{
    private readonly List<MetadataReference> _references;

    public SemanticModelProvider()
    {
        _references = new List<MetadataReference>();
        LoadStandardReferences();
    }

    private void LoadStandardReferences()
    {
        var assemblies = new[]
        {
            typeof(object).Assembly, // System.Private.CoreLib
            typeof(Console).Assembly, // System.Console
            typeof(System.Linq.Enumerable).Assembly, // System.Linq
            typeof(List<>).Assembly, // System.Collections
            Assembly.Load("System.Runtime"),
            Assembly.Load("eQuantic.UI.Core") // eQuantic Core
        };

        foreach (var assembly in assemblies)
        {
            try 
            {
                if (!string.IsNullOrEmpty(assembly.Location))
                {
                    _references.Add(MetadataReference.CreateFromFile(assembly.Location));
                }
            }
            catch 
            { 
                // Ignore assemblies that can't be loaded
            }
        }
    }

    public SemanticModel GetSemanticModel(string sourceCode)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        return GetSemanticModel(tree);
    }

    public SemanticModel GetSemanticModel(SyntaxTree tree)
    {
        var compilation = CSharpCompilation.Create(
            "eQuantic.UI.DynamicAssembly",
            new[] { tree },
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return compilation.GetSemanticModel(tree);
    }
}
