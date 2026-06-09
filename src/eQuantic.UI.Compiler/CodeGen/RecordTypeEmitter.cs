using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// Emits a user value type — a record (positional or with a body) or a struct — as a named JS class
/// with full value semantics: a constructor over the type's value members (with per-member defaults),
/// a structural <c>equals</c> (which <c>$eq.equals</c> delegates to automatically), a prototype-
/// preserving <c>with</c>, a .NET-style <c>toString</c>, and the type's user-declared instance methods
/// (the thing a plain-object representation can't carry). Member names are camelCased.
/// </summary>
public class RecordTypeEmitter
{
    private readonly CSharpToJsConverter _converter;

    public RecordTypeEmitter(CSharpToJsConverter converter) => _converter = converter;

    /// <summary>True for the value types this emitter handles: any record, or a struct, that exposes at
    /// least one value member (positional parameter, auto-property, or public field).</summary>
    public static bool CanEmit(TypeDeclarationSyntax type) =>
        type is RecordDeclarationSyntax or StructDeclarationSyntax && type.ValueMembers().Count > 0;

    /// <summary>
    /// Emits the type as a standalone TypeScript module — the structural <c>equals</c>/<c>with</c> use
    /// <c>$eq</c>, imported from the runtime, and the class is exported so components can import it.
    /// </summary>
    public string EmitModule(TypeDeclarationSyntax type) =>
        "import { $eq } from \"@equantic/runtime\";\n\nexport " + Emit(type) + "\n";

    public string Emit(TypeDeclarationSyntax type)
    {
        var name = type.Identifier.Text;
        var members = type.ValueMembers();

        var sb = new StringBuilder();
        sb.Append($"class {name} {{ ");

        // constructor(x = …, y = …) { this.x = x; this.y = y; } — defaults cover omitted args.
        sb.Append($"constructor({string.Join(", ", members.Select(m => $"{m.Js} = {m.Default}"))}) {{ ");
        foreach (var m in members) sb.Append($"this.{m.Js} = {m.Js}; ");
        sb.Append("} ");

        // Structural equality — $eq.equals(a, b) delegates here when `a` is an instance.
        sb.Append($"equals(o) {{ return o instanceof {name}");
        foreach (var m in members) sb.Append($" && $eq.equals(this.{m.Js}, o.{m.Js})");
        sb.Append("; } ");

        // with(patch): copy preserving the prototype (a spread would drop the methods).
        sb.Append($"with(patch) {{ return new {name}(");
        sb.Append(string.Join(", ", members.Select(m => $"('{m.Js}' in patch ? patch.{m.Js} : this.{m.Js})")));
        sb.Append("); } ");

        // User-declared instance methods.
        var userToString = false;
        foreach (var method in type.Members.OfType<MethodDeclarationSyntax>())
        {
            if (method.Identifier.Text == "ToString") userToString = true;
            sb.Append(EmitMethod(method, name)).Append(' ');
        }

        // .NET record ToString ("Name { X = …, Y = … }") unless the user overrode it.
        if (!userToString)
        {
            var inner = string.Join(", ", members.Select(m => $"{m.Display} = ${{this.{m.Js}}}"));
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
