using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Models;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// Generates TypeScript code from parsed component definitions.
/// Output is designed to be bundled by Bun.
/// </summary>
public class TypeScriptEmitter
{
    private readonly StringBuilder _output = new(); // Legacy, to be removed
    private TypeScriptCodeBuilder _builder = new(); // New builder
    public TypeScriptCodeBuilder.ClassBuilder? ClassBuilder { get; set; }

    // Legacy helper to bridge during refactor
    private void WriteLn(string line = "") => _builder.Line(line);
    private void Indent() => _builder.Indent();
    private void Dedent() => _builder.Dedent();
    private readonly CSharpToJsConverter _converter = new();
    private ComponentDependencyResolver? _dependencyResolver;

    /// <summary>
    /// Sets the dependency resolver for automatic dependency detection
    /// </summary>
    public void SetDependencyResolver(ComponentDependencyResolver resolver)
    {
        _dependencyResolver = resolver;
    }

    public List<TypeScriptCodeBuilder.SourceMapping> GetLastMappings() => _builder.GetMappings();
    
    /// <summary>
    /// Generate TypeScript code for a component
    /// </summary>
    public string Emit(ComponentDefinition component, SemanticModel? semanticModel = null)
    {
        _builder = new TypeScriptCodeBuilder();
        _converter.SetSemanticModel(semanticModel);
        _output.Clear();
        
        EmitImports(component);
        WriteLn();
        
        // Define component class
        var baseClass = component.BaseClassName ?? (component.IsPrimitive ? "HtmlElement" : (component.IsStateful ? "StatefulComponent" : "StatelessComponent"));
        
        // Strip generics from base class name for JS/TS inheritance
        if (baseClass.Contains('<'))
        {
            baseClass = baseClass.Substring(0, baseClass.IndexOf('<'));
        }
        
        _builder.Class(component.Name, baseClass, c => 
            {
                if (component.IsPrimitive)
                {
                    // Emit properties for primitive
                    // WE DO NOT EMIT PROPERTIES AS FIELDS for primitives.
                    // This is because we rely on the base Component constructor to Object.assign(this, props).
                    // If we emit 'fieldName;', it initializes to undefined after super(), overwriting the assigned value.
                    // Only emit methods and constructor.

                    // Emit constructor for primitive
                    // ALWAYS accept props and pass to super, even if C# constructor has no params
                    // This is critical for Object.assign pattern in Component base class
                    if (component.Constructors.Any())
                    {
                        var ctor = component.Constructors.OrderByDescending(c => c.Parameters.Count).First();
                        var hasExplicitParams = ctor.Parameters.Count > 0;

                        string jsParams;
                        if (hasExplicitParams)
                        {
                            // Constructor has explicit params (e.g., Heading(content, level))
                            var paramList = string.Join(", ", ctor.Parameters.Select(p => $"{p.Name}: any"));
                            jsParams = paramList;
                        }
                        else
                        {
                            // Constructor has no params - accept generic props for Object.assign
                            jsParams = "props?: any";
                        }

                        c.Constructor(jsParams, () =>
                        {
                            // Pass props to super
                            c.Raw(hasExplicitParams ? "super();" : "super(props);");

                            // Assign explicit parameters as properties
                            if (hasExplicitParams)
                            {
                                foreach (var param in ctor.Parameters)
                                {
                                    c.Raw($"this.{ToCamelCase(param.Name)} = {param.Name};");
                                }
                            }

                            // Execute C# constructor body (e.g., Direction = FlexDirection.Column)
                            if (ctor.SyntaxNode?.Body != null)
                            {
                                _converter.SetCurrentClass(component.Name);
                                var jsBody = _converter.Convert(ctor.SyntaxNode.Body);
                                jsBody = jsBody.Trim();
                                if (jsBody.StartsWith("{") && jsBody.EndsWith("}"))
                                {
                                    jsBody = jsBody.Substring(1, jsBody.Length - 2).Trim();
                                }
                                if (!string.IsNullOrWhiteSpace(jsBody))
                                {
                                    c.Raw(jsBody, ctor.SyntaxNode.Body);
                                }
                            }
                        });
                    }

                    // Emit Render method for primitive - ONLY if defined or it's the base primitive
                    if (component.BuildMethodNode != null && component.BuildMethodNode.Body != null)
                    {
                        c.Method("render", "", false, () => 
                        {
                            // Discover out variables
                            var outVars = component.BuildMethodNode.Body.DescendantNodes()
                                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.DeclarationExpressionSyntax>()
                                .Select(d => d.Designation.ToString())
                                .Distinct();
                            
                            foreach (var v in outVars)
                            {
                                c.Raw($"let {v};");
                            }

                            _converter.SetCurrentClass(component.Name);
                            var jsBody = _converter.Convert(component.BuildMethodNode.Body);
                            jsBody = jsBody.Trim();
                            if (jsBody.StartsWith("{") && jsBody.EndsWith("}"))
                            {
                                jsBody = jsBody.Substring(1, jsBody.Length - 2).Trim();
                            }
                            c.Raw(jsBody, component.BuildMethodNode.Body);
                        });
                    }
                    else if (component.BaseClassName == "HtmlElement" || component.BaseClassName == null)
                    {
                        // Fallback for base primitives that MUST have a render
                        c.Method("render", "", false, () => 
                        {
                            c.Raw("return { tag: 'div', attributes: {}, events: {}, children: [] };");
                        });
                    }
                }
                else if (component.IsStateful)
                {
                    c.Method("createState", "", false, () => 
                    {
                        c.Raw($"return new {component.StateClassName}(this)");
                    });
                }
                else if (!component.IsAbstract)
                {
                    // For stateless (non-abstract) with positional constructors (Text, Heading, etc.)
                    if (component.Constructors.Any(ctor => ctor.Parameters.Count > 0))
                    {
                        var ctor = component.Constructors.OrderByDescending(ct => ct.Parameters.Count).First();
                        var paramList = string.Join(", ", ctor.Parameters.Select(p => $"{ToCamelCase(p.Name)}?: any"));

                        c.Constructor($"{paramList}, props?: any", () =>
                        {
                            c.Raw("super(props);");
                            // Assign explicit parameters as properties
                            foreach (var param in ctor.Parameters)
                            {
                                var camelName = ToCamelCase(param.Name);
                                c.Raw($"if ({camelName} !== undefined) this.{camelName} = {camelName};");
                            }
                        });
                    }

                    // Build method
                    c.Method("build", "context: BuildContext", false, () =>
                    {
                         if (component.BuildMethodNode != null && component.BuildMethodNode.Body != null)
                         {
                            // Use robust converter for stateless build body
                            _converter.SetCurrentClass(component.Name);
                            var jsBody = _converter.Convert(component.BuildMethodNode.Body);
                            jsBody = jsBody.Trim();
                            if (jsBody.StartsWith("{") && jsBody.EndsWith("}"))
                            {
                                jsBody = jsBody.Substring(1, jsBody.Length - 2).Trim();
                            }
                            c.Raw(jsBody, component.BuildMethodNode.Body);
                         }
                         else if (component.BuildTree != null)
                         {
                             c.Raw("return (");
                             EmitComponentTree(component.BuildTree);
                             c.Raw(");");
                         }
                         else
                         {
                             // Fallback for components without explicit Build method
                             c.Raw("throw new Error('Build method not implemented');");
                         }
                    });
                }
                // Abstract classes: no build method emitted
                
                // Server Actions
                foreach (var action in component.ServerActions)
                {
                    this.ClassBuilder = c;
                    var paramsList = string.Join(", ", action.Parameters.Select(p => $"{p.Name}: {CSharpTypeToTypeScript(p.Type)}"));
                    var argsList = string.Join(", ", action.Parameters.Select(p => p.Name));
                    var returnType = CSharpTypeToTypeScript(action.ReturnType);

                    c.Method(ToCamelCase(action.MethodName), paramsList, true, () => 
                    {
                        c.Raw($"return await getServerActionsClient().invoke('{action.ActionId}', [{argsList}])");
                    }, sourceNode: action.SyntaxNode);
                }
            }, component.TypeParameters);

        // State class logic hooks into builder via EmitStateClass (already refactored)
        // We just need to ensure EmitStateClass writes to builder, OR we inline it here if we want full builder control in one pass.
        // Given current structure, we rely on EmitStateClass using _builder.
        if (component.IsStateful)
        {
            EmitStatefulComponent(component); // This method needs update to NOT use WriteLn manually if we want full builder purity, but for now we mix.
        }
        
        return _builder.ToString();
    }
    
    private void EmitImports(ComponentDefinition component)
    {
        // Core runtime imports
        var coreImports = new HashSet<string> { "Component", "BuildContext", "HtmlElement" };

        if (component.IsStateful)
        {
            coreImports.Add("StatefulComponent");
            coreImports.Add("ComponentState");
        }
        else if (!component.IsPrimitive)
        {
            coreImports.Add("StatelessComponent");
        }

        if (component.ServerActions.Count > 0)
        {
            coreImports.Add("getServerActionsClient");
        }

        // Component imports based on what's used in the component
        var componentTypes = CollectComponentTypes(component.BuildTree);

        // Also scan procedural code in BuildMethodNode
        if (component.BuildMethodNode != null)
        {
             var proceduralTypes = CollectComponentTypesFromNode(component.BuildMethodNode);
             foreach (var t in proceduralTypes) componentTypes.Add(t);
        }

        // CRITICAL: Add base class to component types (for inheritance like "Column extends Flex")
        if (!string.IsNullOrEmpty(component.BaseClassName))
        {
            var baseClass = component.BaseClassName;
            // Clean generic types
            if (baseClass.Contains('<'))
            {
                baseClass = baseClass.Substring(0, baseClass.IndexOf('<'));
            }
            componentTypes.Add(baseClass);
        }

        // AUTOMATIC DEPENDENCY RESOLUTION
        // Use dependency resolver to find transitive dependencies (e.g., Row → Flex)
        if (_dependencyResolver != null)
        {
            var dependencies = _dependencyResolver.ResolveDependencies(componentTypes);
            foreach (var dep in dependencies)
            {
                componentTypes.Add(dep);
            }
        }

        var userComponents = new List<string>();

        foreach (var type in componentTypes)
        {
            var cleanType = type.Trim().Replace("?", "");
            if (cleanType.Contains("<")) cleanType = cleanType.Split('<')[0];

            if (string.IsNullOrEmpty(cleanType) || cleanType == "string" || cleanType == "number" || cleanType == "boolean" || cleanType == "any")
                continue;

            // Skip HtmlNode - it's a type-only interface, not a runtime class
            if (cleanType == "HtmlNode")
                continue;

            if (IsRuntimeComponent(cleanType))
            {
                coreImports.Add(cleanType);
            }
            if (IsRuntimeComponent(cleanType))
            {
                coreImports.Add(cleanType);
            }
            else
            {
                userComponents.Add(cleanType);
            }
        }

        // Check for formatting usage
        if (UsesFormatting(component))
        {
            coreImports.Add("format");
        }

        _builder.Import(coreImports, "@equantic/runtime");

        // Import user components
        foreach (var userComp in userComponents.OrderBy(x => x))
        {
            if (userComp == component.Name) continue;
            _builder.Import(new[] { userComp }, $"./{userComp}");
        }
    }
    
    private bool IsRuntimeComponent(string typeName)
    {
        // Only core runtime types are exported from @equantic/runtime
        // UI components (Box, Button, Text, etc.) are generated and imported from local files
        return typeName switch
        {
            "HtmlNode" or "HtmlStyle" or "ServiceKey" or "ServiceProvider" => true,
            "Component" or "BuildContext" or "HtmlElement" => true,
            "StatefulComponent" or "StatelessComponent" or "ComponentState" => true,
            "getServerActionsClient" or "getRootServiceProvider" => true,
            "StyleBuilder" or "format" => true,
            _ => false
        };
    }

    private bool UsesFormatting(ComponentDefinition component)
    {
        if (component.SyntaxTree == null) return false;
        
        var root = component.SyntaxTree.GetRoot();
        return root.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InterpolatedStringExpressionSyntax>()
            .Any(i => i.Contents.OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InterpolationSyntax>()
                .Any(c => c.FormatClause != null || c.AlignmentClause != null));
    }

    private HashSet<string> CollectComponentTypes(ComponentTree? tree)
    {
        var types = new HashSet<string>();
        if (tree == null) return types;

        types.Add(tree.ComponentType);
        foreach (var child in tree.Children)
        {
            foreach (var t in CollectComponentTypes(child))
            {
                types.Add(t);
            }
        }
        return types;
    }

    private HashSet<string> CollectComponentTypesFromNode(Microsoft.CodeAnalysis.SyntaxNode? node)
    {
        var types = new HashSet<string>();
        if (node == null) return types;
        
        var creations = node.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ObjectCreationExpressionSyntax>();
        foreach (var creation in creations)
        {
             types.Add(creation.Type.ToString());
        }

        var invocations = node.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>();
        foreach (var invocation in invocations)
        {
            if (invocation.Expression is Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax memberAccess)
            {
                if (memberAccess.Expression is Microsoft.CodeAnalysis.CSharp.Syntax.IdentifierNameSyntax identifier)
                {
                     var name = identifier.Identifier.Text;
                     if (!string.IsNullOrEmpty(name) && char.IsUpper(name[0])) 
                     {
                         types.Add(name);
                     }
                }
            }
        }
        return types;
    }
    
    private void EmitStatefulComponent(ComponentDefinition component)
    {
        // Only emit the State class. The Component class is emitted by the main Emit method.
        
        WriteLn();
        
        _builder.Class(component.StateClassName!, "ComponentState", c =>
        {
            // Private component reference
            c.Field("_component", component.Name);
            c.Field("_needsRender", "boolean", "false");
            
            // Typed fields
            foreach (var field in component.StateFields)
            {
                var tsType = CSharpTypeToTypeScript(field.Type);
                var tsDefault = field.DefaultValueNode != null 
                    ? _converter.ConvertExpression(field.DefaultValueNode, field.Type)
                    : ConvertToTsValue(field.DefaultValue ?? GetDefaultForType(field.Type), field.Type);
                c.Field(field.Name, tsType, tsDefault);
            }

            // Constructor
            c.Constructor($"component: {component.Name}", () =>
            {
                c.Raw("super();");
                c.Raw("this._component = component;");
            });
            
            // SetState
            c.Method("setState", "fn: () => void", false, () => 
            {
                c.Raw("fn();");
                c.Raw("this._needsRender = true;");
                c.Raw("this._component._scheduleRender();");
            });

            // Custom methods (Phase 2: Semantic Body)
            foreach (var method in component.Methods)
            {
                EmitMethod(method, c, component.StateClassName);
            }
            
            // Build method
            c.Method("build", "context: BuildContext", false, () =>
            {
                if (component.BuildMethodNode != null && component.BuildMethodNode.Body != null)
                {
                    // Use robust converter to emit full body (supports variables, loops, etc.)
                   _converter.SetCurrentClass(component.StateClassName);
                   var jsBody = _converter.Convert(component.BuildMethodNode.Body);
                   
                   // Remove outer braces since c.Method adds them (via logic or we need to be careful)
                   // Actually c.Method adds braces. Convert(Block) adds braces. 
                   // We should strip the outer braces from jsBody to avoid double indentation/bracing if necessary,
                   // OR just emit the content. 
                   // Let's rely on Convert returning "{ ... }" and we just inject the *content*?
                   // CSharpToJsConverter struct: ConvertBlock returns "{ stmt; stmt; }"
                   // CodeBuilder Method adds "{ ... }". 
                   // So we need to strip first and last char of jsBody.
                   
                   jsBody = jsBody.Trim();
                   if (jsBody.StartsWith("{") && jsBody.EndsWith("}"))
                   {
                       jsBody = jsBody.Substring(1, jsBody.Length - 2).Trim();
                   }
                   c.Raw(jsBody, component.BuildMethodNode.Body);
                }
                else if (component.BuildTree != null)
                {
                    c.Raw("return (");
                    EmitComponentTree(component.BuildTree);
                    c.Raw(");");
                }
                else
                {
                    c.Raw("return new Container({});");
                }
            });
        });
        WriteLn();
    }
    
    private void EmitStatelessComponent(ComponentDefinition component)
    {
        // No-op: Stateless components are fully handled by Emit()
    }
    
    private void EmitMethod(MethodDefinition method, TypeScriptCodeBuilder.ClassBuilder c, string? className = null)
    {
        var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Name}: {CSharpTypeToTypeScript(p.Type)}"));
        var methodName = ToCamelCase(method.Name);
        
        // Lifecycle mapping
        if (method.Name == "OnMount") methodName = "onInit";
        
        var returnType = CSharpTypeToTypeScript(method.ReturnType ?? "void");
        
        var isAsync = method.ReturnType != null && method.ReturnType.StartsWith("Task");
        var asyncPrefix = isAsync ? "async " : "";
        var promiseReturnType = isAsync && returnType == "void" ? "Promise<void>" : 
                                 isAsync ? $"Promise<{returnType}>" : returnType;

        if (method.SyntaxNode != null)
        {
            // Use Robust SyntaxNode Conversion (Phase 2+)
            // Handle body (Block or ExpressionBody)
            string jsBody;
            if (method.SyntaxNode.Body != null)
            {
                _converter.SetCurrentClass(className);
                jsBody = _converter.Convert(method.SyntaxNode.Body);
            }
            else if (method.SyntaxNode.ExpressionBody != null)
            {
                _converter.SetCurrentClass(className);
                var expr = _converter.Convert(method.SyntaxNode.ExpressionBody.Expression);
                jsBody = $"{{ return {expr}; }}";
            }
            else
            {
                jsBody = "{}";
            }
            
            c.Method(methodName, parameters, isAsync, () => {
                var body = jsBody.Trim();
                if (body.StartsWith("{") && body.EndsWith("}"))
                {
                    body = body.Substring(1, body.Length - 2).Trim();
                }
                c.Raw(body);
            }, method.TypeParameters, sourceNode: method.SyntaxNode);
        }
        else
        {
            // Fallback for legacy parsing (should happen rarely now)
            var body = method.Body.Trim().TrimEnd(';');
            _converter.SetCurrentClass(className);
            var convertedExpr = _converter.Convert(body);
            c.Method(methodName, parameters, isAsync, () => {
                c.Raw($"return {convertedExpr};");
            }, method.TypeParameters);
        }
    }
    
    // Helper to access ClassBuilder from TypeScriptEmitter for EmitMethod
    // Actually, I can just pass the ClassBuilder to EmitMethod or store it.
    // Let's refactor EmitMethod to take ClassBuilder.
    
    private void EmitComponentTree(ComponentTree tree)
    {
        Write($"new {tree.ComponentType}({{");
        
        // Extract Key if present. Keys are special and should be top-level in React-like systems,
        // but here we are passing props object to constructor.
        // We need to ensure that if "Key" is in Properties, it is emitted as "key".
        
        var props = tree.Properties.Where(p => p.Key != "Children").ToList();
        
        if (props.Count > 0 || tree.Children.Count > 0)
        {
            WriteLn();
            Indent();
            
            foreach (var (propName, propValue) in props)
            {
                var tsPropName = ToCamelCase(propName);
                if (propName == "Key") tsPropName = "key"; // Special casing for Key

                var tsValue = EmitPropertyValue(propValue);
                WriteLn($"{tsPropName}: {tsValue},");
            }
            
            if (tree.Children.Count > 0)
            {
                Write("children: [");
                WriteLn();
                Indent();
                foreach (var child in tree.Children)
                {
                    EmitComponentTree(child);
                    WriteLn(",");
                }
                Dedent();
                Write("]");
                WriteLn();
            }
            
            Dedent();
            Write("}");
        }
        else
        {
            Write("}");
        }
        
        Write(")");
    }
    
    private string EmitPropertyValue(PropertyValue value)
    {
        return value.Type switch
        {
            PropertyValueType.String => $"'{EscapeString(value.StringValue ?? "")}'",
            PropertyValueType.Number => value.StringValue ?? "0",
            PropertyValueType.Boolean => value.StringValue?.ToLower() ?? "false",
            PropertyValueType.Expression => value.ExpressionNode != null ? _converter.Convert(value.ExpressionNode) : _converter.Convert(value.Expression ?? ""),
            PropertyValueType.EventHandler => ConvertEventHandler(value),
            PropertyValueType.StyleClass => value.Expression ?? "null",
            PropertyValueType.Component when value.ComponentValue != null => EmitComponentToString(value.ComponentValue),
            _ => "null"
        };
    }

    private string ConvertEventHandler(PropertyValue value)
    {
        var expr = value.ExpressionNode != null ? _converter.Convert(value.ExpressionNode) : _converter.Convert(value.Expression ?? "");
        
        // Automatically bind method groups on 'this' (e.g. this.handleClick -> this.handleClick.bind(this))
        if (expr.StartsWith("this.") && !expr.Contains("(") && !expr.Contains("=>") && !expr.Contains(".bind("))
        {
            return $"{expr}.bind(this)";
        }
        return expr;
    }
    
    private string EmitComponentToString(ComponentTree tree)
    {
        var sb = new StringBuilder();
        sb.Append($"new {tree.ComponentType}({{");
        
        var props = tree.Properties.Where(p => p.Key != "Children").ToList();
        foreach (var (propName, propValue) in props)
        {
            var tsPropName = ToCamelCase(propName);
            if (propName == "Key") tsPropName = "key";
            
            sb.Append($" {tsPropName}: {EmitPropertyValue(propValue)},");
        }
        
        if (tree.Children.Count > 0)
        {
            sb.Append(" children: [");
            sb.Append(string.Join(", ", tree.Children.Select(EmitComponentToString)));
            sb.Append("]");
        }
        
        sb.Append(" })");
        return sb.ToString();
    }
    
    private static string CSharpTypeToTypeScript(string? csharpType)
    {
        if (string.IsNullOrEmpty(csharpType)) return "any";
        
        // Handle Nullable<T> or T?
        var isNullable = csharpType.EndsWith("?");
        var baseType = isNullable ? csharpType.Substring(0, csharpType.Length - 1) : csharpType;
        
        if (baseType.StartsWith("Nullable<") && baseType.EndsWith(">"))
        {
            baseType = baseType.Substring(9, baseType.Length - 10);
        }

        string tsType = baseType switch
        {
            "string" => "string",
            "int" or "long" or "double" or "float" or "decimal" or "number" => "number",
            "bool" or "boolean" => "boolean",
            "void" => "void",
            "object" => "any",
            "DateTime" => "Date",
            "Guid" => "string",
            "Task" => "void",
            _ => baseType
        };

        // Handle Generics (limited support)
        if (tsType.StartsWith("List<") && tsType.EndsWith(">"))
        {
            var itemType = tsType.Substring(5, tsType.Length - 6);
            tsType = $"{CSharpTypeToTypeScript(itemType)}[]";
        }
        else if (tsType.StartsWith("IEnumerable<") && tsType.EndsWith(">"))
        {
            var itemType = tsType.Substring(12, tsType.Length - 13);
            tsType = $"{CSharpTypeToTypeScript(itemType)}[]";
        }
        else if (tsType.StartsWith("Task<") && tsType.EndsWith(">"))
        {
            var itemType = tsType.Substring(5, tsType.Length - 6);
            tsType = CSharpTypeToTypeScript(itemType);
        }
        else if (tsType.StartsWith("Action<") && tsType.EndsWith(">"))
        {
            var itemType = tsType.Substring(7, tsType.Length - 8);
            tsType = $"({ToCamelCase(itemType)}: {CSharpTypeToTypeScript(itemType)}) => void";
        }
        else if (tsType == "Action")
        {
            tsType = "() => void";
        }
        else if (tsType.StartsWith("Dictionary<") && tsType.EndsWith(">"))
        {
            tsType = "Record<string, any>";
        }

        return tsType;
    }
    
    private static string ConvertToTsValue(string value, string type)
    {
        if (value.Contains("new()") || value.Contains("new List"))
        {
            var tsType = CSharpTypeToTypeScript(type);
            if (tsType.EndsWith("[]"))
            {
                return "[]";
            }
        }
        
        return type.ToLowerInvariant() switch
        {
            "string" => $"\"{value.Trim('"')}\"",
            "int" or "double" or "float" => value,
            "bool" or "boolean" => value.ToLower(),
             _ => value
        };
    }
    
    private static string GetDefaultForType(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "string" => "\"\"",
            "int" or "double" or "float" => "0",
            "bool" or "boolean" => "false",
             _ => "null"
        };
    }
    
    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
    
    private static string EscapeString(string s)
    {
        return s.Replace("'", "\\'").Replace("\n", "\\n");
    }
    
    #region Output Helpers
    
    private void Write(string text)
    {
        _builder.Line(text); // Basic mapping for Write, though Builder prefers structured calls
    }
    
    #endregion
}
