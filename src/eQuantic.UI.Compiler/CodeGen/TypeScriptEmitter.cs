using System.Text;
using eQuantic.UI.Compiler.Models;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// Generates TypeScript code from parsed component definitions.
/// Output is designed to be bundled by Bun.
/// </summary>
public class TypeScriptEmitter
{
    private readonly StringBuilder _output = new();
    private int _indentLevel = 0;
    private readonly CSharpToJsConverter _converter = new();
    
    /// <summary>
    /// Generate TypeScript code for a component
    /// </summary>
    public string Emit(ComponentDefinition component)
    {
        _output.Clear();
        _indentLevel = 0;
        
        // Emit imports
        EmitImports(component);
        WriteLn();
        
        // Emit component class
        if (component.IsStateful)
        {
            EmitStatefulComponent(component);
        }
        else
        {
            EmitStatelessComponent(component);
        }
        
        return _output.ToString();
    }
    
    private void EmitImports(ComponentDefinition component)
    {
        // Core runtime imports
        var coreImports = new List<string> { "Component", "BuildContext" };
        
        if (component.IsStateful)
        {
            coreImports.Add("StatefulComponent");
        }
        else
        {
            coreImports.Add("StatelessComponent");
        }
        
        WriteLn($"import {{ {string.Join(", ", coreImports)} }} from '@equantic/runtime';");
        
        // Widget imports based on what's used in the component
        var widgetTypes = CollectWidgetTypes(component.BuildTree);
        if (widgetTypes.Count > 0)
        {
            WriteLn($"import {{ {string.Join(", ", widgetTypes.OrderBy(x => x))} }} from '@equantic/runtime';");
        }
    }
    
    private HashSet<string> CollectWidgetTypes(ComponentTree? tree)
    {
        var types = new HashSet<string>();
        if (tree == null) return types;
        
        types.Add(tree.ComponentType);
        foreach (var child in tree.Children)
        {
            foreach (var t in CollectWidgetTypes(child))
            {
                types.Add(t);
            }
        }
        return types;
    }
    
    private void EmitStatefulComponent(ComponentDefinition component)
    {
        // Component class with TypeScript types
        WriteLn($"export class {component.Name} extends StatefulComponent {{");
        Indent();
        WriteLn($"createState(): {component.StateClassName} {{");
        Indent();
        WriteLn($"return new {component.StateClassName}(this);");
        Dedent();
        WriteLn("}");
        Dedent();
        WriteLn("}");
        WriteLn();
        
        // State class with type annotations
        WriteLn($"class {component.StateClassName} {{");
        Indent();
        
        // Private component reference
        WriteLn($"private _component: {component.Name};");
        WriteLn("private _needsRender: boolean = false;");
        WriteLn();
        
        // Typed fields
        foreach (var field in component.StateFields)
        {
            var tsType = CSharpTypeToTypeScript(field.Type);
            var tsDefault = ConvertToTsValue(field.DefaultValue ?? GetDefaultForType(field.Type), field.Type);
            WriteLn($"private {field.Name}: {tsType} = {tsDefault};");
        }
        WriteLn();
        
        // Constructor with type
        WriteLn($"constructor(component: {component.Name}) {{");
        Indent();
        WriteLn("this._component = component;");
        Dedent();
        WriteLn("}");
        WriteLn();
        
        // SetState method
        WriteLn("setState(fn: () => void): void {");
        Indent();
        WriteLn("fn();");
        WriteLn("this._needsRender = true;");
        WriteLn("this._component._scheduleRender();");
        Dedent();
        WriteLn("}");
        WriteLn();
        
        // Custom methods
        foreach (var method in component.Methods)
        {
            EmitMethod(method);
            WriteLn();
        }
        
        // Build method
        WriteLn("build(context: BuildContext): Component {");
        Indent();
        if (component.BuildTree != null)
        {
            Write("return ");
            EmitComponentTree(component.BuildTree);
            WriteLn(";");
        }
        else
        {
            WriteLn("return null!;");
        }
        Dedent();
        WriteLn("}");
        
        Dedent();
        WriteLn("}");
    }
    
    private void EmitStatelessComponent(ComponentDefinition component)
    {
        WriteLn($"export class {component.Name} extends StatelessComponent {{");
        Indent();
        
        WriteLn("build(context: BuildContext): Component {");
        Indent();
        if (component.BuildTree != null)
        {
            Write("return ");
            EmitComponentTree(component.BuildTree);
            WriteLn(";");
        }
        else
        {
            WriteLn("return null!;");
        }
        Dedent();
        WriteLn("}");
        
        Dedent();
        WriteLn("}");
    }
    
    private void EmitMethod(MethodDefinition method)
    {
        var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Name}: {CSharpTypeToTypeScript(p.Type)}"));
        var methodName = ToCamelCase(method.Name);
        var returnType = CSharpTypeToTypeScript(method.ReturnType ?? "void");
        
        var body = method.Body.Trim().TrimEnd(';');
        
        if (body.StartsWith("{"))
        {
            var jsBody = _converter.Convert(body);
            WriteLn($"{methodName}({parameters}): {returnType} {jsBody}");
        }
        else if (body.Contains("=>"))
        {
            var arrowIndex = body.IndexOf("=>");
            var expression = body[(arrowIndex + 2)..].Trim();
            
            if (expression.StartsWith("SetState"))
            {
                var innerStart = expression.IndexOf("() =>");
                if (innerStart >= 0)
                {
                    var innerExpr = expression[(innerStart + 5)..].Trim();
                    if (innerExpr.EndsWith(")")) innerExpr = innerExpr[..^1].Trim();
                    
                    var convertedExpr = _converter.Convert(innerExpr);
                    WriteLn($"{methodName}({parameters}): {returnType} {{ this.setState(() => {{ {convertedExpr}; }}); }}");
                }
                else
                {
                    var convertedExpr = _converter.Convert(expression);
                    WriteLn($"{methodName}({parameters}): {returnType} {{ {convertedExpr}; }}");
                }
            }
            else
            {
                var convertedExpr = _converter.Convert(expression);
                if (expression.Contains("=") || expression.Contains("++") || expression.Contains("--"))
                {
                    WriteLn($"{methodName}({parameters}): {returnType} {{ this.setState(() => {{ {convertedExpr}; }}); }}");
                }
                else
                {
                    WriteLn($"{methodName}({parameters}): {returnType} {{ {convertedExpr}; }}");
                }
            }
        }
        else
        {
            var convertedExpr = _converter.Convert(body);
            WriteLn($"{methodName}({parameters}): {returnType} {{ {convertedExpr}; }}");
        }
    }
    
    private void EmitComponentTree(ComponentTree tree)
    {
        Write($"new {tree.ComponentType}({{");
        
        var props = tree.Properties.Where(p => p.Key != "Children").ToList();
        
        if (props.Count > 0 || tree.Children.Count > 0)
        {
            WriteLn();
            Indent();
            
            foreach (var (propName, propValue) in props)
            {
                var tsPropName = ToCamelCase(propName);
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
            PropertyValueType.Expression => _converter.Convert(value.Expression ?? ""),
            PropertyValueType.EventHandler => _converter.Convert(value.Expression ?? ""),
            PropertyValueType.StyleClass => value.Expression ?? "null",
            PropertyValueType.Component when value.ComponentValue != null => EmitComponentToString(value.ComponentValue),
            _ => "null"
        };
    }
    
    private string EmitComponentToString(ComponentTree tree)
    {
        var sb = new StringBuilder();
        sb.Append($"new {tree.ComponentType}({{");
        
        var props = tree.Properties.Where(p => p.Key != "Children").ToList();
        foreach (var (propName, propValue) in props)
        {
            sb.Append($" {ToCamelCase(propName)}: {EmitPropertyValue(propValue)},");
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
    
    private static string CSharpTypeToTypeScript(string csharpType)
    {
        return csharpType.ToLowerInvariant() switch
        {
            "string" => "string",
            "int" or "double" or "float" or "decimal" or "long" or "short" or "byte" => "number",
            "bool" or "boolean" => "boolean",
            "void" => "void",
            "object" => "unknown",
            _ when csharpType.StartsWith("List<") => $"{CSharpTypeToTypeScript(csharpType[5..^1])}[]",
            _ when csharpType.EndsWith("[]") => $"{CSharpTypeToTypeScript(csharpType[..^2])}[]",
            _ => csharpType // Keep as-is for custom types
        };
    }
    
    private static string ConvertToTsValue(string value, string type)
    {
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
        _output.Append(text);
    }
    
    private void WriteLn(string text = "")
    {
        if (!string.IsNullOrEmpty(text))
        {
            _output.Append(new string(' ', _indentLevel * 2));
            _output.AppendLine(text);
        }
        else
        {
            _output.AppendLine();
        }
    }
    
    private void Indent() => _indentLevel++;
    private void Dedent() => _indentLevel = Math.Max(0, _indentLevel - 1);
    
    #endregion
}
