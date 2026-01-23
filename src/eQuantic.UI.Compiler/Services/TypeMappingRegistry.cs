using System.Text.RegularExpressions;

namespace eQuantic.UI.Compiler.Services;

public class TypeMappingRegistry
{
    private readonly Dictionary<string, string> _methodMappings = new();
    private readonly Dictionary<string, string> _typeMappings = new();
    private readonly Dictionary<string, string> _propertyMappings = new();

    public TypeMappingRegistry()
    {
        InitializeDefaults();
    }

    private void InitializeDefaults()
    {
        // Core Methods
        RegisterMethod("System.Console.WriteLine", "console.log");
        RegisterMethod("System.String.IsNullOrEmpty", "!"); // Special prefix operator
        RegisterMethod("System.Object.ToString", "toString");
        
        // State
        RegisterMethod("SetState", "this.setState");

        // Collections (Phase 1 - Simple name matching)
        RegisterMethod("Add", "push");
        RegisterMethod("Clear", "length = 0");
        RegisterMethod("Count", "length");
        
        // Properties
        RegisterProperty("Length", "length");
        RegisterProperty("Count", "length");
    }

    public void RegisterMethod(string csharpName, string jsName) => _methodMappings[csharpName] = jsName;
    public void RegisterType(string csharpName, string jsName) => _typeMappings[csharpName] = jsName;
    public void RegisterProperty(string csharpName, string jsName) => _propertyMappings[csharpName] = jsName;

    public string? GetMethodMapping(string methodName)
    {
        return _methodMappings.TryGetValue(methodName, out var mapping) ? mapping : null;
    }

    public string? GetPropertyMapping(string propertyName)
    {
        return _propertyMappings.TryGetValue(propertyName, out var mapping) ? mapping : null;
    }
}
