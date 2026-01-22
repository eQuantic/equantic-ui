using System.Text;
using eQuantic.UI.Compiler.Models;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// Generates JavaScript code from parsed component definitions
/// </summary>
public class JavaScriptEmitter
{
    private readonly StringBuilder _output = new();
    private int _indentLevel = 0;
    
    /// <summary>
    /// Generate JavaScript code for a component
    /// </summary>
    public string Emit(ComponentDefinition component)
    {
        _output.Clear();
        _indentLevel = 0;
        
        // Emit imports
        EmitImports();
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
    
    private void EmitImports()
    {
        WriteLn("import { StatefulComponent, StatelessComponent, Container, Text, Button, TextInput, Row, Column, Flex } from '@equantic/ui-runtime';");
    }
    
    private void EmitStatefulComponent(ComponentDefinition component)
    {
        // Component class
        WriteLn($"export class {component.Name} extends StatefulComponent {{");
        Indent();
        WriteLn("createState() {");
        Indent();
        WriteLn($"return new {component.StateClassName}(this);");
        Dedent();
        WriteLn("}");
        Dedent();
        WriteLn("}");
        WriteLn();
        
        // State class
        WriteLn($"class {component.StateClassName} {{");
        Indent();
        
        // Fields
        foreach (var field in component.StateFields)
        {
            var jsDefault = ConvertToJsValue(field.DefaultValue ?? "null", field.Type);
            WriteLn($"{field.Name} = {jsDefault};");
        }
        WriteLn();
        
        // Constructor
        WriteLn("constructor(component) {");
        Indent();
        WriteLn("this._component = component;");
        WriteLn("this._needsRender = false;");
        Dedent();
        WriteLn("}");
        WriteLn();
        
        // SetState method
        WriteLn("setState(fn) {");
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
        WriteLn("build(context) {");
        Indent();
        if (component.BuildTree != null)
        {
            Write("return ");
            EmitComponentTree(component.BuildTree);
            WriteLn(";");
        }
        else
        {
            WriteLn("return null;");
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
        
        WriteLn("build(context) {");
        Indent();
        if (component.BuildTree != null)
        {
            Write("return ");
            EmitComponentTree(component.BuildTree);
            WriteLn(";");
        }
        else
        {
            WriteLn("return null;");
        }
        Dedent();
        WriteLn("}");
        
        Dedent();
        WriteLn("}");
    }
    
    private void EmitMethod(MethodDefinition method)
    {
        var parameters = string.Join(", ", method.Parameters.Select(p => p.Name));
        var methodName = ToCamelCase(method.Name);
        
        // Check if it's an arrow function expression like: () => SetState(() => _count++)
        if (method.Body.Contains("=>"))
        {
            // Extract expression after =>
            var arrowIndex = method.Body.IndexOf("=>");
            var expression = method.Body[(arrowIndex + 2)..].Trim();
            
            // Check if expression is SetState(() => something)
            if (expression.StartsWith("SetState"))
            {
                // Extract inner lambda from SetState(() => _count++)
                var innerStart = expression.IndexOf("() =>");
                if (innerStart >= 0)
                {
                    var innerExpr = expression[(innerStart + 5)..].Trim().TrimEnd(')');
                    innerExpr = ConvertFieldAccess(innerExpr);
                    WriteLn($"{methodName}({parameters}) {{ this.setState(() => {{ {innerExpr}; }}); }}");
                }
                else
                {
                    // Fallback
                    expression = ConvertCSharpToJs(expression);
                    WriteLn($"{methodName}({parameters}) {{ {expression}; }}");
                }
            }
            else
            {
                // Simple expression like _count++
                expression = ConvertCSharpToJs(expression);
                WriteLn($"{methodName}({parameters}) {{ this.setState(() => {{ {expression}; }}); }}");
            }
        }
        else
        {
            WriteLn($"{methodName}({parameters}) {{");
            Indent();
            var jsBody = ConvertCSharpToJs(method.Body);
            WriteLn(jsBody);
            Dedent();
            WriteLn("}");
        }
    }
    
    private string ConvertFieldAccess(string expression)
    {
        // Convert _fieldName to this._fieldName (without double conversion)
        return System.Text.RegularExpressions.Regex.Replace(
            expression, 
            @"(?<!this\.)(?<![a-zA-Z])_([a-zA-Z]+)", 
            "this._$1");
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
                var jsPropName = ToCamelCase(propName);
                var jsValue = EmitPropertyValue(propValue);
                WriteLn($"{jsPropName}: {jsValue},");
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
            PropertyValueType.String => EmitStringValue(value.StringValue ?? ""),
            PropertyValueType.Number => value.StringValue ?? "0",
            PropertyValueType.Boolean => value.StringValue?.ToLower() ?? "false",
            PropertyValueType.Expression => ConvertExpressionToJs(value.Expression ?? ""),
            PropertyValueType.EventHandler => ConvertLambdaToJs(value.Expression ?? ""),
            PropertyValueType.StyleClass => value.Expression ?? "null",
            PropertyValueType.Component when value.ComponentValue != null => EmitComponentToString(value.ComponentValue),
            _ => "null"
        };
    }
    
    private string EmitStringValue(string value)
    {
        // Remove surrounding quotes if present
        value = value.Trim('"');
        return $"'{EscapeString(value)}'";
    }
    
    private string ConvertExpressionToJs(string expression)
    {
        // Check if it's an interpolated string
        if (expression.StartsWith("$\"") || expression.StartsWith("$@\""))
        {
            return ConvertInterpolatedString(expression);
        }
        
        // Check if it's a simple field access
        if (expression.StartsWith("_"))
        {
            return ConvertFieldAccess(expression);
        }
        
        return ConvertCSharpToJs(expression);
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
    
    private string ConvertCSharpToJs(string csharp)
    {
        if (string.IsNullOrEmpty(csharp)) return csharp;
        
        var result = csharp;
        
        // Convert C# interpolated strings to JS template literals
        // $"Message: {_message}" -> `Message: ${this._message}`
        result = ConvertInterpolatedString(result);
        
        // Convert C# dictionary initializers
        // new() { ["testid"] = "counter" } -> { testid: "counter" }
        result = ConvertDictionaryInitializer(result);
        
        // Convert SetState calls
        result = result.Replace("SetState", "this.setState");
        
        // Convert field access - only standalone underscores not already prefixed with this.
        // Use word boundary to avoid double conversion
        result = System.Text.RegularExpressions.Regex.Replace(
            result, 
            @"(?<!this\.)(?<![a-zA-Z])_([a-zA-Z]+)", 
            "this._$1");
        
        return result;
    }
    
    private string ConvertLambdaToJs(string lambda)
    {
        if (string.IsNullOrEmpty(lambda)) return lambda;
        
        var result = lambda;
        
        // Convert SetState calls
        result = result.Replace("SetState", "this.setState");
        
        // Convert field access with word boundary check
        result = System.Text.RegularExpressions.Regex.Replace(
            result, 
            @"(?<!this\.)(?<![a-zA-Z])_([a-zA-Z]+)", 
            "this._$1");
        
        return result;
    }
    
    private string ConvertInterpolatedString(string input)
    {
        // Match $"..." or $@"..."
        var pattern = @"\$""([^""]*?)""";
        return System.Text.RegularExpressions.Regex.Replace(input, pattern, match =>
        {
            var content = match.Groups[1].Value;
            
            // Convert {expression} to ${expression}
            content = System.Text.RegularExpressions.Regex.Replace(
                content, 
                @"\{([^}]+)\}", 
                m =>
                {
                    var expr = m.Groups[1].Value;
                    // Add this. prefix to fields
                    expr = System.Text.RegularExpressions.Regex.Replace(
                        expr, 
                        @"(?<![a-zA-Z])_([a-zA-Z]+)", 
                        "this._$1");
                    return "${" + expr + "}";
                });
            
            return "`" + content + "`";
        });
    }
    
    private string ConvertDictionaryInitializer(string input)
    {
        // Match new() { ["key"] = "value" } or new Dictionary<...>() { ... }
        var pattern = @"new\s*\(\)\s*\{\s*\[""([^""]+)""\]\s*=\s*""([^""]+)""\s*\}";
        return System.Text.RegularExpressions.Regex.Replace(input, pattern, match =>
        {
            var key = match.Groups[1].Value;
            var value = match.Groups[2].Value;
            return $"{{ {key}: \"{value}\" }}";
        });
    }

    private string ConvertMethodBody(string body)
    {
        if (string.IsNullOrEmpty(body)) return body;
        
        // Handle arrow expressions: () => _count++
        if (body.Contains("=>"))
        {
            // Extract the expression after =>
            var arrowIndex = body.IndexOf("=>");
            var expression = body[(arrowIndex + 2)..].Trim();
            
            // Convert the expression
            expression = ConvertCSharpToJs(expression);
            
            return expression;
        }
        
        return ConvertCSharpToJs(body);
    }

    private string ConvertToJsValue(string value, string type)
    {
        return type switch
        {
            "string" => $"\"{value.Trim('"')}\"",
            "int" or "double" or "float" => value,
            "bool" => value.ToLower(),
            _ => value
        };
    }
    
    private string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
    
    private string EscapeString(string s)
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
