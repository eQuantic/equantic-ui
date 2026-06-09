using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// Emits a positional C# record (record class / record struct) as a named JS class with full value
/// semantics: a constructor over the positional parameters, a structural <c>equals</c> (which
/// <c>$eq.equals</c> delegates to automatically), a prototype-preserving <c>with</c>, a .NET-style
/// <c>toString</c>, and the record's user-declared instance methods (the thing a plain-object
/// representation can't carry). Field names are camelCased to match member access elsewhere.
/// </summary>
public class RecordTypeEmitter
{
    private readonly CSharpToJsConverter _converter;

    public RecordTypeEmitter(CSharpToJsConverter converter) => _converter = converter;

    /// <summary>True for the records this emitter handles today — positional (with a parameter list).</summary>
    public static bool CanEmit(RecordDeclarationSyntax rec) => rec.ParameterList is { Parameters.Count: > 0 };

    /// <summary>
    /// Emits the record as a standalone TypeScript module — the structural <c>equals</c>/<c>with</c> use
    /// <c>$eq</c>, imported from the runtime, and the class is exported so components can import it.
    /// </summary>
    public string EmitModule(RecordDeclarationSyntax rec) =>
        "import { $eq } from \"@equantic/runtime\";\n\nexport " + Emit(rec) + "\n";

    public string Emit(RecordDeclarationSyntax rec)
    {
        var name = rec.Identifier.Text;
        var fields = rec.ParameterList!.Parameters
            .Select(p => (Display: p.Identifier.Text, Js: p.Identifier.Text.ToCamelCase()))
            .ToList();

        var sb = new StringBuilder();
        sb.Append($"class {name} {{ ");

        // constructor(x, y) { this.x = x; this.y = y; }
        sb.Append($"constructor({string.Join(", ", fields.Select(f => f.Js))}) {{ ");
        foreach (var f in fields) sb.Append($"this.{f.Js} = {f.Js}; ");
        sb.Append("} ");

        // Structural equality — $eq.equals(a, b) delegates here when `a` is an instance.
        sb.Append($"equals(o) {{ return o instanceof {name}");
        foreach (var f in fields) sb.Append($" && $eq.equals(this.{f.Js}, o.{f.Js})");
        sb.Append("; } ");

        // with(patch): copy preserving the prototype (a spread would drop the methods).
        sb.Append($"with(patch) {{ return new {name}(");
        sb.Append(string.Join(", ", fields.Select(f => $"('{f.Js}' in patch ? patch.{f.Js} : this.{f.Js})")));
        sb.Append("); } ");

        // User-declared instance methods.
        var userToString = false;
        foreach (var method in rec.Members.OfType<MethodDeclarationSyntax>())
        {
            if (method.Identifier.Text == "ToString") userToString = true;
            sb.Append(EmitMethod(method, name)).Append(' ');
        }

        // .NET record ToString ("Name { X = …, Y = … }") unless the user overrode it.
        if (!userToString)
        {
            var inner = string.Join(", ", fields.Select(f => $"{f.Display} = ${{this.{f.Js}}}"));
            sb.Append($"toString() {{ return `{name} {{ {inner} }}`; }} ");
        }

        sb.Append('}');
        return sb.ToString();
    }

    private string EmitMethod(MethodDeclarationSyntax method, string className)
    {
        var jsName = method.Identifier.Text.ToCamelCase();
        var pars = string.Join(", ", method.ParameterList.Parameters.Select(p => p.Identifier.Text.ToCamelCase()));
        _converter.SetCurrentClass(className);

        string body;
        if (method.Body != null)
        {
            var block = _converter.Convert(method.Body).Trim(); // "{ … }"
            body = block.StartsWith("{") && block.EndsWith("}") ? block[1..^1].Trim() : block;
        }
        else if (method.ExpressionBody != null)
        {
            body = $"return {_converter.Convert(method.ExpressionBody.Expression)};";
        }
        else
        {
            body = "";
        }

        return $"{jsName}({pars}) {{ {body} }}";
    }
}
