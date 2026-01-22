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
    
    private readonly CSharpToJsConverter _converter = new();

    private void EmitMethod(MethodDefinition method)
    {
        var parameters = string.Join(", ", method.Parameters.Select(p => p.Name));
        var methodName = ToCamelCase(method.Name);
        
        // Remove trailing semicolons from body if present
        var body = method.Body.Trim().TrimEnd(';');
        
        if (body.StartsWith("{"))
        {
            // Block body
            var jsBody = _converter.Convert(body);
            // Convert private field access in blocks manually for now if needed, 
            // but the converter handles blocks
            WriteLn($"{methodName}({parameters}) {jsBody}");
        }
        else if (body.Contains("=>"))
        {
            // Arrow function: () => SetState(() => _count++)
            var arrowIndex = body.IndexOf("=>");
            var expression = body[(arrowIndex + 2)..].Trim();
            
            // Handle SetState specially
            if (expression.StartsWith("SetState"))
            {
                // Extract inner lambda from SetState(() => expr)
                var innerStart = expression.IndexOf("() =>");
                if (innerStart >= 0)
                {
                    var innerExpr = expression[(innerStart + 5)..].Trim();
                    if (innerExpr.EndsWith(")")) innerExpr = innerExpr[..^1].Trim();
                    
                    var convertedExpr = _converter.Convert(innerExpr);
                    WriteLn($"{methodName}({parameters}) {{ this.setState(() => {{ {convertedExpr}; }}); }}");
                }
                else
                {
                    var convertedExpr = _converter.Convert(expression);
                    WriteLn($"{methodName}({parameters}) {{ {convertedExpr}; }}");
                }
            }
            else
            {
                // Simple expression
                 var convertedExpr = _converter.Convert(expression);
                 
                 // If it causes state change (assignment/increment), wrap in SetState?
                 if (expression.Contains("=") || expression.Contains("++") || expression.Contains("--"))
                 {
                     WriteLn($"{methodName}({parameters}) {{ this.setState(() => {{ {convertedExpr}; }}); }}");
                 }
                 else
                 {
                     WriteLn($"{methodName}({parameters}) {{ {convertedExpr}; }}");
                 }
            }
        }
        else
        {
            // Expression body: _increment() => _count++
            // But parsed as body string usually
            var convertedExpr = _converter.Convert(body);
            WriteLn($"{methodName}({parameters}) {{ {convertedExpr}; }}");
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
    
    private string ConvertLambdaToJs(string lambda)
    {
        // Use the converter for lambdas too
        return _converter.Convert(lambda);
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
