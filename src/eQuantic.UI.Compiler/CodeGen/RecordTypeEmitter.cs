using System.Collections.Generic;
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
    public string EmitModule(TypeDeclarationSyntax type)
    {
        var imports = new StringBuilder("import { $eq } from \"@equantic/runtime\";\n");
        // A base record is emitted as its own module — import it so `extends` resolves.
        var (baseName, _, _) = BaseInfo(type);
        if (baseName != null) imports.Append($"import {{ {baseName} }} from \"./{baseName}\";\n");
        return imports.Append("\nexport ").Append(Emit(type)).Append('\n').ToString();
    }

    public string Emit(TypeDeclarationSyntax type)
    {
        var name = type.Identifier.Text;
        var members = type.ValueMembers();
        var (baseName, superArgs, passedToBase) = BaseInfo(type);

        var sb = new StringBuilder();
        sb.Append($"class {name}{(baseName != null ? $" extends {baseName}" : "")} {{ ");

        // TYPE-ONLY member declarations: they restore checking on `record.x` without emitting any runtime
        // code (the constructor below does the assigning). Only OWN members are declared — the ones passed
        // to the base record's primary constructor are already declared by the base module.
        foreach (var m in members)
            if (!passedToBase.Contains(m.Display)) sb.Append($"declare {m.Js}: {m.TsType}; ");

        // constructor(x = …, y = …) { [super(…);] this.<own> = …; } — defaults cover omitted args;
        // members passed to the base record's primary constructor are assigned by `super`, not here.
        sb.Append($"constructor({string.Join(", ", members.Select(m => $"{m.Js} = {m.Default}"))}) {{ ");
        if (baseName != null) sb.Append($"super({superArgs}); ");
        foreach (var m in members)
            if (!passedToBase.Contains(m.Display)) sb.Append($"this.{m.Js} = {m.Js}; ");
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

    /// <summary>
    /// The base record (if any) from a primary-constructor base clause (<c>record Dog(…) : Animal(Name)</c>):
    /// its name (generics erased), the JS <c>super(...)</c> arguments, and which members are passed to the
    /// base (so they aren't re-assigned in the derived constructor). Interfaces / non-record bases yield none.
    /// </summary>
    private (string? BaseName, string SuperArgs, HashSet<string> PassedToBase) BaseInfo(TypeDeclarationSyntax type)
    {
        var primary = type.BaseList?.Types.OfType<PrimaryConstructorBaseTypeSyntax>().FirstOrDefault();
        if (primary == null) return (null, "", new HashSet<string>());

        var baseName = primary.Type.ToString();
        if (baseName.Contains('<')) baseName = baseName[..baseName.IndexOf('<')]; // erase generics

        var passed = new HashSet<string>();
        var superArgs = new List<string>();
        if (primary.ArgumentList != null)
        {
            foreach (var arg in primary.ArgumentList.Arguments)
            {
                if (arg.Expression is IdentifierNameSyntax id)
                {
                    passed.Add(id.Identifier.Text);                 // a member forwarded to the base
                    superArgs.Add(id.Identifier.Text.ToCamelCase());
                }
                else
                {
                    superArgs.Add(_converter.ConvertExpression(arg.Expression));
                }
            }
        }
        return (baseName, string.Join(", ", superArgs), passed);
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
