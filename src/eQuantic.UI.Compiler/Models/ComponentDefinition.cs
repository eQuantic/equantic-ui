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
    /// Generic type parameters
    /// </summary>
    public List<string> TypeParameters { get; set; } = new();
    
    /// <summary>
    /// Whether it's a stateful (has state) or stateless component
    /// </summary>
    public bool IsStateful { get; set; }

    /// <summary>
    /// True for the SHARED stateful shape (<c>eQuantic.UI.Primitives.StatefulComponent</c>): state as
    /// fields on the component itself, direct <c>SetState</c> — no <c>CreateState</c>/state-class split.
    /// Emitted extending the runtime's <c>SharedStatefulComponent</c>; structurally handled like a
    /// stateless component otherwise (<see cref="IsStateful"/> stays false).
    /// </summary>
    public bool IsSharedStateful { get; set; }
    
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
    /// Fields declared directly on the component class itself (static data, consts, instance fields) —
    /// distinct from <see cref="StateFields"/> which belong to a stateful component's state class.
    /// Emitted as class members on the component.
    /// </summary>
    public List<StateField> ComponentFields { get; set; } = new();

    /// <summary>
    /// Methods defined in the state class
    /// </summary>
    public List<MethodDefinition> Methods { get; set; } = new();
    
    /// <summary>
    /// Properties defined in the class
    /// </summary>
    public List<PropertyDefinition> Properties { get; set; } = new();

    /// <summary>
    /// Constructors defined in the class
    /// </summary>
    public List<MethodDefinition> Constructors { get; set; } = new();
    
    /// <summary>
    /// The Build method's component tree (Legacy/Fallback)
    /// </summary>
    public ComponentTree? BuildTree { get; set; }

    /// <summary>
    /// The full Build method syntax node (Preferred)
    /// </summary>
    public Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax? BuildMethodNode { get; set; }
    
    /// <summary>
    /// StyleClass usages found in the component
    /// </summary>
    public List<StyleClassUsage> StyleUsages { get; set; } = new();

    /// <summary>
    /// Indicates if this is a primitive HTML component (inherits from HtmlElement)
    /// </summary>
    public bool IsPrimitive { get; set; }

    /// <summary>
    /// Indicates if this is an abstract class
    /// </summary>
    public bool IsAbstract { get; set; }

    /// <summary>
    /// Base class name
    /// </summary>
    public string? BaseClassName { get; set; }
    
    /// <summary>
    /// Source file path
    /// </summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>
    /// Roslyn Syntax Reference
    /// </summary>
    public Microsoft.CodeAnalysis.SyntaxTree? SyntaxTree { get; set; }

    /// <summary>
    /// Runtime helpers used by the component (ClassBuilder, StyleBuilder, etc.)
    /// </summary>
    public HashSet<string> UsedHelpers { get; set; } = new();

    /// <summary>
    /// True when this definition is a user value type (a record or struct) emitted as a named JS class
    /// rather than a UI component. Discovered by scanning for record/struct declarations.
    /// </summary>
    public bool IsRecordType { get; set; }

    /// <summary>
    /// The record/struct declaration syntax, when <see cref="IsRecordType"/> is true.
    /// </summary>
    public Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax? ValueTypeSyntax { get; set; }

    /// <summary>
    /// True for a C# <c>static class</c> utility (e.g. <c>static class Format { … }</c>) used from a
    /// component (<c>Format.Foo()</c>). Emitted as its own TS module of static members; <see cref="ValueTypeSyntax"/>
    /// holds the declaration.
    /// </summary>
    public bool IsStaticHelper { get; set; }

    /// <summary>
    /// Simple names of referenced types the RUNTIME provides (today: the shared vocabulary in
    /// <c>eQuantic.UI.Primitives</c> — Box/Row/Text/…, tokens, ButtonStyles). Discovered per file via the
    /// semantic model (namespace-based, no fixed list), excluding enums (they lower to string literals).
    /// The emitter imports these from <c>@equantic/runtime</c> instead of <c>./&lt;Type&gt;</c> modules.
    /// </summary>
    public HashSet<string> RuntimeProvidedTypes { get; set; } = new();

    /// <summary>
    /// Simple names of referenced ENUM types (semantic-model discovered). Enum members lower to
    /// camelCase string literals, so these names never appear as identifiers in emitted code — the
    /// emitter must not import them.
    /// </summary>
    public HashSet<string> EnumTypes { get; set; } = new();
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
    public List<string> TypeParameters { get; set; } = new();
    public List<ParameterDefinition> Parameters { get; set; } = new();
    public bool IsAsync { get; set; }
    public Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax? SyntaxNode { get; set; }
}

/// <summary>
/// Represents a state field
/// </summary>
public class StateField
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? DefaultValue { get; set; }
    public Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionSyntax? DefaultValueNode { get; set; }
    /// <summary>True for <c>static</c>/<c>const</c> fields — emitted as a <c>static</c> class member and
    /// referenced as <c>ClassName.field</c> rather than <c>this.field</c>.</summary>
    public bool IsStatic { get; set; }
}

/// <summary>
/// Represents a method definition
/// </summary>
public class MethodDefinition
{
    public string Name { get; set; } = string.Empty;
    public string ReturnType { get; set; } = "void";
    public List<string> TypeParameters { get; set; } = new();
    public List<ParameterDefinition> Parameters { get; set; } = new();
    public string Body { get; set; } = string.Empty;
    /// <summary>A <c>static</c> helper belongs to the CLASS — call sites are qualified with the class
    /// name (<c>Users.initials(…)</c>), so emitting it on the prototype would break them at runtime.</summary>
    public bool IsStatic { get; set; }
    public Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax? SyntaxNode { get; set; }
    /// <summary>The body block — works for constructors too (whose declaration isn't a
    /// <see cref="MethodDeclarationSyntax"/>), so the emitter can transpile and run a ctor's body.</summary>
    public Microsoft.CodeAnalysis.CSharp.Syntax.BlockSyntax? BodyNode { get; set; }
}

/// <summary>
/// Represents a property definition
/// </summary>
public class PropertyDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? DefaultValue { get; set; }
    public Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionSyntax? DefaultValueNode { get; set; }
    public bool IsPublic { get; set; }
    public bool IsStatic { get; set; }
    /// <summary>
    /// The JS literal for C#'s implicit <c>default(T)</c> on a property with NO initializer, when leaving
    /// it <c>undefined</c> would change behavior. An unset C# enum property is its zero member, and the
    /// lowered form is a string (<c>'none'</c>) that code compares with <c>===</c> — so `undefined` silently
    /// takes the other branch and the client diverges from SSR. Null when the C# default and `undefined`
    /// behave alike (reference types, and the falsy value types).
    /// </summary>
    public string? ImplicitDefaultJs { get; set; }
    /// <summary>The declaration syntax — lets the emitter inspect a computed property's expression body
    /// (<c>X =&gt; expr</c>) or get/set accessor bodies, distinguishing auto-properties from computed ones.</summary>
    public Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax? Node { get; set; }
}

/// <summary>
/// Represents a method parameter
/// </summary>
public class ParameterDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    /// <summary>The C# default value expression (<c>Variant variant = Variant.Primary</c>) — emitted as a
    /// JS default parameter so optional constructor arguments keep their C# semantics.</summary>
    public Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionSyntax? DefaultValueNode { get; set; }
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
    public Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionSyntax? ExpressionNode { get; set; }
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
