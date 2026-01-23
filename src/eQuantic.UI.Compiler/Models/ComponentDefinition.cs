namespace eQuantic.UI.Compiler.Models;

/// <summary>
/// Represents a parsed component from a .cs file
/// </summary>
public class ComponentDefinition
{
    /// <summary>
    /// Namespace of the component
    /// </summary>
    public string Namespace { get; set; } = string.Empty;
    
    /// <summary>
    /// Name of the component class
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether it's a stateful (has state) or stateless component
    /// </summary>
    public bool IsStateful { get; set; }
    
    /// <summary>
    /// Name of the state class (for stateful components)
    /// </summary>
    public string? StateClassName { get; set; }
    
    /// <summary>
    /// Page routes from [Page] attributes
    /// </summary>
    public List<PageRouteInfo> PageRoutes { get; set; } = new();
    
    /// <summary>
    /// Server actions from [ServerAction] methods
    /// </summary>
    public List<ServerActionInfo> ServerActions { get; set; } = new();
    
    /// <summary>
    /// State fields (field name -> type)
    /// </summary>
    public List<StateField> StateFields { get; set; } = new();
    
    /// <summary>
    /// Methods defined in the state class
    /// </summary>
    public List<MethodDefinition> Methods { get; set; } = new();
    
    /// <summary>
    /// The Build method's component tree
    /// </summary>
    public ComponentTree? BuildTree { get; set; }
    
    /// <summary>
    /// StyleClass usages found in the component
    /// </summary>
    public List<StyleClassUsage> StyleUsages { get; set; } = new();
    
    /// <summary>
    /// Source file path
    /// </summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>
    /// Roslyn Syntax Reference
    /// </summary>
    public Microsoft.CodeAnalysis.SyntaxTree? SyntaxTree { get; set; }
}

/// <summary>
/// Page route information from [Page] attribute
/// </summary>
public class PageRouteInfo
{
    public string Route { get; set; } = string.Empty;
    public string? Title { get; set; }
}

/// <summary>
/// Server action information from [ServerAction] attribute
/// </summary>
public class ServerActionInfo
{
    public string MethodName { get; set; } = string.Empty;
    public string ActionId { get; set; } = string.Empty;
    public string ReturnType { get; set; } = "void";
    public List<ParameterDefinition> Parameters { get; set; } = new();
    public bool IsAsync { get; set; }
}

/// <summary>
/// Represents a state field
/// </summary>
public class StateField
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? DefaultValue { get; set; }
}

/// <summary>
/// Represents a method definition
/// </summary>
public class MethodDefinition
{
    public string Name { get; set; } = string.Empty;
    public string ReturnType { get; set; } = "void";
    public List<ParameterDefinition> Parameters { get; set; } = new();
    public string Body { get; set; } = string.Empty;
}

/// <summary>
/// Represents a method parameter
/// </summary>
public class ParameterDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// Represents the component tree from Build method
/// </summary>
public class ComponentTree
{
    public string ComponentType { get; set; } = string.Empty;
    public Dictionary<string, PropertyValue> Properties { get; set; } = new();
    public List<ComponentTree> Children { get; set; } = new();
}

/// <summary>
/// Represents a property value in component tree
/// </summary>
public class PropertyValue
{
    public PropertyValueType Type { get; set; }
    public string? StringValue { get; set; }
    public string? Expression { get; set; }
    public ComponentTree? ComponentValue { get; set; }
    public List<ComponentTree>? ListValue { get; set; }
}

public enum PropertyValueType
{
    String,
    Number,
    Boolean,
    Expression, // C# expression like _count++
    Component,
    ComponentList,
    StyleClass,
    EventHandler
}

/// <summary>
/// Represents a StyleClass usage
/// </summary>
public class StyleClassUsage
{
    public string FullName { get; set; } = string.Empty; // e.g., "AppStyles.Button"
    public string ClassName { get; set; } = string.Empty;
    public string PropertyPath { get; set; } = string.Empty;
}
