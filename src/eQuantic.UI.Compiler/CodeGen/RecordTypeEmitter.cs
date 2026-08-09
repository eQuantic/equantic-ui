using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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

    /// <summary>A model that can answer about THIS declaration. Roslyn throws for a node from
    /// another tree, so the COMPILATION is asked for that tree's own model; when even it does not
    /// know the tree, the literal rules still apply.</summary>
    private SemanticModel? ModelFor(SyntaxNode node)
    {
        if (_converter.Model is not { } model) return null;
        if (ReferenceEquals(node.SyntaxTree, model.SyntaxTree)) return model;
        return model.Compilation.ContainsSyntaxTree(node.SyntaxTree)
            ? model.Compilation.GetSemanticModel(node.SyntaxTree)
            : null;
    }

    /// <summary>Whether the emitted class carries VALUE semantics (structural equals, `with`).</summary>
    private static bool IsValueShape(TypeDeclarationSyntax type) =>
        type is RecordDeclarationSyntax or StructDeclarationSyntax;

    /// <summary>
    /// Emits the type as a standalone TypeScript module — the structural <c>equals</c>/<c>with</c> use
    /// <c>$eq</c>, imported from the runtime, and the class is exported so components can import it.
    /// </summary>
    /// <param name="tsTypeDeclarations">TypeScript output. FALSE is plain JavaScript, and it has to
    /// reach here: a record emitted with `declare x: string` and `constructor(x: any = null)` is a
    /// TypeScript file, so a consumer that runs the module directly — the playground, which asks
    /// the compiler for plain JS — gets "Unexpected identifier" at import time. That failure is
    /// invisible from the outside: compilation reports success, and the page renders a blank
    /// frame.</param>
    public string EmitModule(TypeDeclarationSyntax type, bool tsTypeDeclarations = true)
    {
        // What the BODIES reference, not just what the members declare: a computed property
        // (`InsertedRange => new CodeRange(new CodePosition(…))`) names types the positional
        // members never mention, and an unimported name is `Cannot find name 'CodePosition'`.
        var runtimeProvided = new HashSet<string> { "$eq" };
        if (ModelFor(type) is { } model)
            Services.RuntimeProvidedTypeScanner.Collect(type, model, runtimeProvided, new HashSet<string>());
        runtimeProvided.Remove(type.Identifier.Text);

        var body = Emit(type, tsTypeDeclarations);
        // Only what the emitted text actually NAMES: a type mentioned in the C# and erased on the
        // way out (an interface, an enum) would otherwise import a name nothing uses, which the
        // runtime's own build rejects.
        var used = runtimeProvided
            // Lookarounds, not `\b`: `$eq` starts with a non-word character, so a word boundary
            // never matches it and the one import every module needs would be the first to go.
            .Where(name => System.Text.RegularExpressions.Regex.IsMatch(body,
                $@"(?<![\w$]){System.Text.RegularExpressions.Regex.Escape(name)}(?![\w$])"))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var imports = new StringBuilder(
            $"import {{ {string.Join(", ", used)} }} from \"@equantic/runtime\";\n");
        // A base record is emitted as its own module — import it so `extends` resolves.
        var (baseName, _, _) = BaseInfo(type);
        if (baseName != null) imports.Append($"import {{ {baseName} }} from \"./{baseName}\";\n");
        return imports.Append("\nexport ").Append(body).Append('\n').ToString();
    }

    /// <summary>
    /// The OTHER constructors — `CodeRange(CodePosition caret) : this(caret, caret)`. JavaScript has
    /// one constructor, so each alternate becomes a branch on how many arguments actually arrived:
    /// bind its own parameters from the slots they landed in, evaluate the `: this(…)` arguments,
    /// and land those in the primary's.
    /// <para>
    /// Dropping them is what used to happen, and it was silent: `new CodeRange(caret)` left the
    /// second member NULL, so every read of it threw somewhere far away from the constructor.
    /// </para>
    /// </summary>
    private string ChainedOverloads(TypeDeclarationSyntax type, IReadOnlyList<ValueMember> members)
    {
        var sb = new StringBuilder();
        foreach (var ctor in type.Members.OfType<ConstructorDeclarationSyntax>())
        {
            if (ctor.Initializer is not { } chain
                || !chain.ThisOrBaseKeyword.IsKind(SyntaxKind.ThisKeyword)) continue;
            var arity = ctor.ParameterList.Parameters.Count;
            if (arity == 0 || arity > members.Count) continue;

            _converter.SetCurrentClass(type.Identifier.Text);
            sb.Append($"if (arguments.length === {arity}) {{ ");
            // The alternate's parameters ARE the arguments that arrived, in the primary's slots.
            for (var i = 0; i < arity; i++)
                sb.Append($"const {ctor.ParameterList.Parameters[i].Identifier.Text.ToJsIdentifier()} = {members[i].Js}; ");
            // Evaluate first, assign after: an argument that reads a slot it also writes must see
            // the value that arrived, not the one this loop just put there.
            var args = chain.ArgumentList.Arguments;
            for (var i = 0; i < args.Count && i < members.Count; i++)
                sb.Append($"const $c{i} = {_converter.ConvertExpression(args[i].Expression)}; ");
            for (var i = 0; i < args.Count && i < members.Count; i++)
                sb.Append($"{members[i].Js} = $c{i}; ");
            sb.Append("} ");
        }
        return sb.ToString();
    }

    /// <summary>The TS annotation for a declared type, resolved the way the class emitter does it:
    /// an enum is its member string and an interface has no emitted twin to name.</summary>
    private string TsTypeOf(TypeSyntax? type)
    {
        if (type is null) return "any";
        var resolved = ModelFor(type)?.GetTypeInfo(type).Type;
        return resolved switch
        {
            { TypeKind: TypeKind.Enum } => "string",
            { TypeKind: TypeKind.Interface } => "any",
            _ => TypeDeclarationExtensions.TsTypeFor(type, ModelFor(type)),
        };
    }

    /// <param name="type">The record/struct declaration to emit.</param>
    /// <param name="tsTypeDeclarations">Emit the TYPE-ONLY <c>declare</c> member declarations. They are
    /// TypeScript syntax (no runtime code — they restore checking on <c>record.x</c> for the .ts module
    /// path) and MUST stay off for plain-JS consumers: the conformance harness executes the emitted
    /// class as <c>.mjs</c>, where <c>declare</c> is a parse error.</param>
    public string Emit(TypeDeclarationSyntax type, bool tsTypeDeclarations = false)
    {
        _converter.EmitTypeAnnotations(tsTypeDeclarations);
        var name = type.Identifier.Text;
        var members = type.ValueMembers(ModelFor(type));
        var (baseName, superArgs, passedToBase) = BaseInfo(type);

        var sb = new StringBuilder();
        sb.Append($"class {name}{(baseName != null ? $" extends {baseName}" : "")} {{ ");

        // TYPE-ONLY member declarations: they restore checking on `record.x` without emitting any runtime
        // code (the constructor below does the assigning). Only OWN members are declared — the ones passed
        // to the base record's primary constructor are already declared by the base module.
        if (tsTypeDeclarations)
            foreach (var m in members)
                if (!passedToBase.Contains(m.Display)) sb.Append($"declare {m.Js}: {m.TsType}; ");

        // constructor(x = …, y = …) { [super(…);] this.<own> = …; } — defaults cover omitted args;
        // members passed to the base record's primary constructor are assigned by `super`, not here.
        // TS mode annotates ctor params (`label: any = null`) — a bare `= null` default would make
        // TypeScript infer the param TYPE as `null`. Plain-JS mode stays annotation-free (.mjs).
        sb.Append(tsTypeDeclarations
            ? $"constructor({string.Join(", ", members.Select(m => $"{m.Js}: any = {m.Default}"))}) {{ "
            : $"constructor({string.Join(", ", members.Select(m => $"{m.Js} = {m.Default}"))}) {{ ");
        if (baseName != null) sb.Append($"super({superArgs}); ");
        sb.Append(ChainedOverloads(type, members));
        foreach (var m in members)
            if (!passedToBase.Contains(m.Display)) sb.Append($"this.{m.Js} = {m.Js}; ");
        sb.Append("} ");

        // VALUE semantics belong to records and structs. A plain class is IDENTITY: giving it a
        // structural `equals` would make two different buckets compare equal, and a `with` would
        // hand back a copy where the caller expects the same object.
        if (type is RecordDeclarationSyntax or StructDeclarationSyntax)
        {
            // Structural equality — $eq.equals(a, b) delegates here when `a` is an instance.
            // Param annotations are TS-only (the plain-JS path must stay parseable as .mjs).
            sb.Append(tsTypeDeclarations ? $"equals(o: unknown) {{ return o instanceof {name}"
                : $"equals(o) {{ return o instanceof {name}");
            foreach (var m in members) sb.Append($" && $eq.equals(this.{m.Js}, o.{m.Js})");
            sb.Append("; } ");

            // with(patch): copy preserving the prototype (a spread would drop the methods).
            sb.Append(tsTypeDeclarations ? $"with(patch: any) {{ return new {name}(" : $"with(patch) {{ return new {name}(");
            sb.Append(string.Join(", ", members.Select(m => $"('{m.Js}' in patch ? patch.{m.Js} : this.{m.Js})")));
            sb.Append("); } ");
        }

        // User-declared methods — a STATIC one keeps its modifier: a record's factory
        // (`LegalBlock.P(…)`, `Money.Zero()`) is called on the CLASS, and emitting it as an
        // instance method turns every call site into "X.p is not a function" at hydration.
        var userToString = false;
        foreach (var method in type.Members.OfType<MethodDeclarationSyntax>())
        {
            if (method.Identifier.Text == "ToString") userToString = true;
            sb.Append(EmitMethod(method, name, tsTypeDeclarations)).Append(' ');
        }

        // OPERATOR overloads. JavaScript cannot overload `+`, so the operator becomes a static
        // method and the call site is rewritten to call it — dropping it silently made `a + b` on
        // two objects concatenate their toString()s, which is wrong output with nothing to see.
        foreach (var op in type.Members.OfType<OperatorDeclarationSyntax>())
        {
            var opName = OperatorMethodName(op.OperatorToken.Text);
            if (opName is null) continue;
            var pars = string.Join(", ", op.ParameterList.Parameters
                .Select(p => tsTypeDeclarations
                    ? $"{p.Identifier.Text.ToJsIdentifier()}: {TsTypeOf(p.Type)}"
                    : p.Identifier.Text.ToJsIdentifier()));
            var body = op.ExpressionBody is { } expr
                ? $"return {_converter.ConvertExpression(expr.Expression)};"
                : op.Body is { } block ? Unwrap(_converter.Convert(block)) : "";
            sb.Append($"static {opName}({pars}) {{ {body} }} ");
        }

        // Static FIELDS — `public static readonly CodePosition Start = new(0, 0);`. The other half of
        // the "well-known value" idiom, and nothing emitted them: `CodePosition.start` was
        // undefined, so every comparison against the origin silently failed.
        foreach (var field in type.Members.OfType<FieldDeclarationSyntax>())
        {
            if (!field.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword) || m.IsKind(SyntaxKind.ConstKeyword)))
                continue;
            _converter.SetCurrentClass(name);
            foreach (var variable in field.Declaration.Variables)
            {
                var value = variable.Initializer is { } init
                    ? _converter.ConvertExpression(init.Value, field.Declaration.Type.ToString())
                    : "undefined";
                sb.Append($"static {variable.Identifier.Text.ToCamelCase()} = {value}; ");
            }
        }

        // PROPERTIES with a body — computed, on the instance (`Start => Anchor <= Focus ? … : …`)
        // or static (`static Foo Empty => …`, the factory idiom). A record is a value with
        // BEHAVIOUR; emitting only its positional members threw the behaviour away.
        foreach (var property in type.Members.OfType<PropertyDeclarationSyntax>())
        {
            var isStatic = property.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));
            var getter = property.ExpressionBody?.Expression
                ?? property.AccessorList?.Accessors
                    .FirstOrDefault(a => a.Keyword.Text == "get")?.ExpressionBody?.Expression;
            _converter.SetCurrentClass(name);
            var prefix = isStatic ? "static " : "";
            var propertyName = property.Identifier.Text.ToCamelCase();
            if (getter is not null)
            {
                sb.Append($"{prefix}get {propertyName}() {{ return ")
                  .Append(_converter.Convert(getter))
                  .Append("; } ");
                continue;
            }
            if (property.AccessorList?.Accessors
                    .FirstOrDefault(a => a.Keyword.Text == "get")?.Body is { } block)
            {
                sb.Append($"{prefix}get {propertyName}() {{ ")
                  .Append(Unwrap(_converter.Convert(block)))
                  .Append(" } ");
            }
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

    /// <summary>
    /// The static-method name an operator becomes. Named for the operation, not for the CLR's
    /// <c>op_Addition</c> mangling: the emitted class is read by people too.
    /// </summary>
    internal static string? OperatorMethodName(string token) => token switch
    {
        "+" => "opAdd",
        "-" => "opSubtract",
        "*" => "opMultiply",
        "/" => "opDivide",
        "%" => "opModulo",
        "==" => "opEquals",
        "!=" => "opNotEquals",
        "<" => "opLessThan",
        ">" => "opGreaterThan",
        "<=" => "opLessOrEqual",
        ">=" => "opGreaterOrEqual",
        _ => null,
    };

    /// <summary>A converted block comes back braced; a method body wants its contents.</summary>
    private static string Unwrap(string block)
    {
        var trimmed = block.Trim();
        return trimmed.StartsWith('{') && trimmed.EndsWith('}') ? trimmed[1..^1].Trim() : trimmed;
    }

    private string EmitMethod(MethodDeclarationSyntax method, string className,
        bool tsTypeDeclarations)
    {
        var jsName = method.Identifier.Text.ToCamelCase();
        // Typed: a record is a VALUE and its methods are its behaviour — untyped parameters put an
        // `any` at every one of them and quietly ended the checking on the way in. Only in the .ts
        // emission, though: the conformance harness runs the same class as plain `.mjs`, where an
        // annotation is a parse error rather than a type.
        var pars = string.Join(", ", method.ParameterList.Parameters
            .Select(p => tsTypeDeclarations
                ? $"{p.Identifier.Text.ToCamelCase()}: {TsTypeOf(p.Type)}"
                : p.Identifier.Text.ToCamelCase()));
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

        var isStatic = method.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)) ? "static " : "";
        return $"{isStatic}{jsName}({pars}) {{ {body} }}";
    }
}
