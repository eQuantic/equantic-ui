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
    /// The component's IDENTITY at the seam with the server: the CLR full name of its definition
    /// (<c>MyApp.Pages.Row</c>, <c>Outer+Inner</c>, <c>Grid`1</c>), exactly what
    /// <c>eQuantic.UI.Web.ComponentIdentity.Of</c> answers for the same type at runtime. Emitted as
    /// <c>static $typeId</c>; the runtime keys a component's hydration state by it (#278).
    /// </summary>
    public string TypeIdentity { get; set; } = string.Empty;

    /// <summary>
    /// Generic type parameters
    /// </summary>
    public List<string> TypeParameters { get; set; } = new();
    
    /// <summary>
    /// The stateful shape: state as fields on the component itself, mutated through
    /// <c>SetState</c>. Emitted extending the runtime's <c>StatefulComponent</c>; structurally
    /// parsed like a stateless component (Build + ctors + methods + fields on the class).
    /// </summary>
    public bool IsStateful { get; set; }

    /// <summary>
    /// Page routes from [Page] attributes
    /// </summary>
    public List<PageRouteInfo> PageRoutes { get; set; } = new();
    
    /// <summary>
    /// Server actions from [ServerAction] methods
    /// </summary>
    public List<ServerActionInfo> ServerActions { get; set; } = new();
    
    /// <summary>
    /// Fields declared directly on the component class itself (static data, consts, instance
    /// fields). Emitted as class members on the component.
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
    /// The full Build method syntax node (Preferred)
    /// </summary>
    public Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax? BuildMethodNode { get; set; }
    

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
    /// True when this component declares no <c>Build</c> of its own AND the base it is written over
    /// is ANOTHER COMPONENT that supplies one — so the twin must emit no <c>build</c> at all and let
    /// JavaScript's own prototype chain answer.
    ///
    /// <para>
    /// The emitter otherwise falls back to <c>throw new Error('Build method not implemented')</c>,
    /// which is the honest stub over an abstract framework base and a REGRESSION over an app-owned
    /// one: it overrides a working inherited <c>build</c> with a throw, so a component that renders
    /// today would die at first render. Measured while collapsing the parser's classification arms —
    /// the old fourth arm hid this by marking such a class primitive instead.
    /// </para>
    /// </summary>
    public bool BuildComesFromTheBase { get; set; }
    
    /// <summary>
    /// Source file path
    /// </summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>
    /// Roslyn Syntax Reference
    /// </summary>
    public Microsoft.CodeAnalysis.SyntaxTree? SyntaxTree { get; set; }

    /// <summary>
    /// The class a component was parsed from. A name is not an identity: two classes of one name in
    /// two namespaces of one file are two types, and a search of the tree by name found the first, so
    /// a page's own members were checked as another class's.
    /// </summary>
    public Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax? ClassSyntax { get; set; }

    /// <summary>
    /// Runtime helpers used by the component (ClassBuilder, StyleBuilder, etc.)
    /// </summary>
    public HashSet<string> UsedHelpers { get; set; } = new();

    /// <summary>
    /// True when this definition is a user value type (a record or struct) emitted as a named JS class
    /// rather than a UI component. Discovered by scanning for record/struct declarations.
    /// </summary>
    public bool IsRecordType { get; set; }

    /// <summary>C# 15 <c>union Pet(Cat, Dog);</c> — emitted as a TS union alias module; the case
    /// types ride in <see cref="ValueTypeSyntax"/>'s parameter list.</summary>
    public bool IsUnionType { get; set; }

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
    /// A plain class the app declares — not a record, not static, not a component. It emits as its
    /// own module of INSTANCE members: identity, not value, so no structural equals and no `with`.
    /// </summary>
    public bool IsPlainClass { get; set; }

    /// <summary>
    /// Simple names of referenced types the RUNTIME provides (today: the shared vocabulary in
    /// <c>eQuantic.UI.Primitives</c> — Box/Row/Text/…, tokens — and the shared component libraries).
    /// Discovered per file via the semantic model (namespace-based, no fixed
    /// list), excluding enums (they lower to string literals).
    /// The emitter imports these from <c>@equantic/runtime</c> instead of <c>./&lt;Type&gt;</c> modules.
    /// </summary>
    public HashSet<string> RuntimeProvidedTypes { get; set; } = new();

    /// <summary>
    /// HOST-ONLY vocabulary types this component NAMED, and where — kept out of
    /// <see cref="RuntimeProvidedTypes"/> because the runtime ships no export for them.
    /// <para>
    /// The parser finds them (a type POSITION is only visible to a semantic sweep) and the emitter
    /// reports them, because the parser has no diagnostics channel and the emitter does. Before
    /// this, `public Matrix2D Placement { get; init; }` on a component compiled, emitted
    /// `import { Matrix2D } from "@equantic/runtime"`, and took the page down at hydration.
    /// </para>
    /// </summary>
    public Dictionary<string, Microsoft.CodeAnalysis.SyntaxNode> HostOnlyTypes { get; } = new();

    /// <summary>
    /// Simple names of referenced ENUM types (semantic-model discovered). Enum members lower to
    /// camelCase string literals, so these names never appear as identifiers in emitted code — the
    /// emitter must not import them.
    /// </summary>
    public HashSet<string> EnumTypes { get; set; } = new();

    /// <summary>
    /// Whether <see cref="RuntimeProvidedTypes"/> was actually populated by a semantic model. When
    /// FALSE (standalone <c>CompileSource</c>, no references — the playground's mode), the emitter
    /// cannot trust that an unlisted type is user code: it falls back to <see cref="DeclaredInSource"/>
    /// as the user universe.
    /// </summary>
    public bool ResolvedSemantically { get; set; }

    /// <summary>
    /// Simple names of every type DECLARED in the same source text (classes, records, structs,
    /// enums, interfaces). In a standalone compilation the source is the user's whole universe —
    /// a referenced type that is not in here can only come from the runtime vocabulary.
    /// </summary>
    public HashSet<string> DeclaredInSource { get; set; } = new();

    /// <summary>
    /// Simple names of referenced APP-LEVEL types — declared in this compilation's own source
    /// (semantic-model discovered), as opposed to framework/BCL types that arrive as metadata.
    /// Needed because a type reached only through a STATIC MEMBER (<c>Brand.Violet</c>) is invisible
    /// to the syntactic collectors; the emitter imports the ones that actually became modules.
    /// </summary>
    public HashSet<string> AppTypes { get; set; } = new();
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
    /// <summary>The declared type SYNTAX — what the semantic model binds to a symbol, so the
    /// emitter can compute the field's hydration spec from the type itself (never its spelling).</summary>
    public Microsoft.CodeAnalysis.CSharp.Syntax.TypeSyntax? TypeNode { get; set; }
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

    /// <summary>
    /// An <c>override</c> — the author REPLACING a base member on purpose, which is how a component
    /// is written (<c>Build</c>). Everything else that lands on a runtime member's name replaces it
    /// by accident, and the two are indistinguishable once emitted: a plain <c>void Mount()</c>
    /// becomes <c>mount()</c> over the runtime's own, so the component never mounts (EQ2011).
    /// </summary>
    public bool IsOverride { get; set; }
    public Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax? SyntaxNode { get; set; }
    /// <summary>The body block — works for constructors too (whose declaration isn't a
    /// <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax"/>), so the emitter can transpile and run a ctor's body.</summary>
    public Microsoft.CodeAnalysis.CSharp.Syntax.BlockSyntax? BodyNode { get; set; }
    /// <summary>
    /// The other half a body can take: <c>public Chart(x) =&gt; _x = x;</c>. Held beside
    /// <see cref="BodyNode"/> because a constructor written this way has no block, and reading only
    /// the block dropped the author's wiring on the floor — the twin ran a constructor that did
    /// nothing while the C# one assigned a field. Form decides first, then behaviour.
    /// </summary>
    public Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionSyntax? ExpressionBodyNode { get; set; }
    /// <summary>
    /// A C# 12 PRIMARY constructor, whose parameters are implicit fields rather than locals: members
    /// read them as <c>this.name</c>, so whatever the constructor puts in one has to land there.
    /// </summary>
    public bool IsPrimaryConstructor { get; set; }
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

    /// <summary>
    /// Whether this is a DEPENDENCY rather than data. A component's constructor takes what it draws
    /// — a label, a variant, a callback — and a page's takes what it needs to work: a photo
    /// library, a ledger, a configuration. Those are interfaces, always, and that is the rule: an
    /// interface parameter is resolved from the container instead of being passed by the caller,
    /// which is exactly what ActivatorUtilities does natively.
    /// </summary>
    public bool IsService { get; set; }

    /// <summary>
    /// Whether the component declared it cannot work without this capability (`IClock` rather than
    /// `IClock?`). An absent one is then a mistake worth naming — on the target where it is absent,
    /// at the seam, instead of a null that fails somewhere inside the component's own code.
    /// </summary>
    public bool IsRequiredService { get; set; }

    /// <summary>The interface's own name — the key both sides agree on, since a C# type does not
    /// exist at run time in the browser but its name does.</summary>
    public string? ServiceKey { get; set; }
}


