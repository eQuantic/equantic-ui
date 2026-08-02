using eQuantic.UI.Codegen;
using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// The writer for the file type this compiler emits: TypeScript. Indentation, lines and blocks come
/// from the shared <see cref="CodeWriter"/> — what belongs HERE is what only a TypeScript file has:
/// imports, classes, members, and the source mapping that lets a browser show C#.
/// </summary>
public class TypeScriptCodeBuilder
{
    private readonly CodeWriter _writer = new();
    private readonly List<SourceMapping> _mappings = new();

    public struct SourceMapping
    {
        public int GeneratedLine;
        public int GeneratedColumn;
        public int SourceLine;
        public int SourceColumn;
        public string SourceFile;
    }

    public List<SourceMapping> GetMappings() => _mappings;

    public void Import(IEnumerable<string> items, string from)
    {
        if (!items.Any()) return;
        var sortedItems = items.OrderBy(i => i);
        Write($"import {{ {string.Join(", ", sortedItems)} }} from \"{from}\";");
    }

    public void Class(string name, string? baseClass, Action<ClassBuilder> buildAction, IEnumerable<string>? typeParameters = null, SyntaxNode? sourceNode = null, bool export = true)
    {
        if (sourceNode != null) RecordMapping(sourceNode);
        var generics = typeParameters != null && typeParameters.Any() ? $"<{string.Join(", ", typeParameters)}>" : "";
        var extendsClause = string.IsNullOrEmpty(baseClass) ? "" : $" extends {baseClass}";

        // The closing brace travels with the scope: a class body that opens and never closes is the
        // one bug generated code reliably ships, and a `using` makes it unrepresentable.
        using (_writer.BeginBlock($"{(export ? "export " : "")}class {name}{generics}{extendsClause} {{"))
        {
            buildAction(new ClassBuilder(this));
        }
        Write("");
    }

    public void Indent() => _writer.IndentLevel++;

    public void Dedent() => _writer.IndentLevel = Math.Max(0, _writer.IndentLevel - 1);

    /// <summary>A line, optionally carrying the C# it came from into the source map.</summary>
    public void Line(string line, SyntaxNode? sourceNode = null)
    {
        if (sourceNode != null) RecordMapping(sourceNode);
        Write(line);
    }

    /// <summary>An EMPTY line carries no indentation — trailing whitespace is a diff nobody wants.</summary>
    private void Write(string line)
    {
        if (string.IsNullOrEmpty(line)) _writer.AppendLine();
        else _writer.AppendLine(line);
    }

    private void RecordMapping(SyntaxNode node)
    {
        var pos = node.GetLocation().GetLineSpan();
        _mappings.Add(new SourceMapping
        {
            GeneratedLine = _writer.CurrentLine,
            // 0-based column where the emitted line's content begins (after indentation).
            GeneratedColumn = _writer.IndentLevel * 4,
            // Roslyn line/character positions are already 0-based, matching the source-map spec.
            SourceLine = pos.StartLinePosition.Line,
            SourceColumn = pos.StartLinePosition.Character,
            SourceFile = pos.Path
        });
    }

    public override string ToString() => _writer.ToString();

    public class ClassBuilder(TypeScriptCodeBuilder builder)
    {
        private readonly TypeScriptCodeBuilder _builder = builder;

        /// <param name="isDeclare">Emit a TYPE-ONLY field (<c>declare x: T;</c>) — no runtime code at all.
        /// Required for properties populated from outside the class body (the base
        /// <c>Object.assign(props)</c>): under <c>useDefineForClassFields</c> a plain declaration would
        /// define the field as <c>undefined</c> after <c>super()</c> and wipe the assigned value.</param>
        /// <summary>A field. A null <paramref name="type"/> means NO annotation — a plain class is
        /// JavaScript, and a C# type name (<c>Func&lt;T, bool&gt;</c>) would be neither.</summary>
        public void Field(string name, string? type, string? defaultValue = null, SyntaxNode? sourceNode = null, bool isStatic = false, bool isDeclare = false)
        {
            var init = defaultValue != null ? $" = {defaultValue}" : "";
            var prefix = (isDeclare ? "declare " : "") + (isStatic ? "static " : "");
            var annotation = string.IsNullOrEmpty(type) ? "" : $": {type}";
            _builder.Line($"{prefix}{name}{annotation}{init};", sourceNode);
        }

        public void Property(string name, string type, bool isPublic = true, SyntaxNode? sourceNode = null)
        {
            var access = isPublic ? "" : "private ";
            _builder.Line($"{access}{name}: {type};", sourceNode);
        }

        public void Constructor(string parameters, Action bodyAction, SyntaxNode? sourceNode = null) =>
            Member($"constructor({parameters}) {{", bodyAction, sourceNode);

        public void Method(string name, string parameters, bool isAsync, Action bodyAction, IEnumerable<string>? typeParameters = null, SyntaxNode? sourceNode = null, bool isStatic = false, bool isGenerator = false)
        {
            // Iterator methods (yield) are JS GENERATORS: the `*` marker is what lets the emitted
            // `yield` statements parse. `async *` is the async-iterator form, also valid JS.
            var prefix = (isStatic ? "static " : "") + (isAsync ? "async " : "") + (isGenerator ? "*" : "");
            var generics = typeParameters != null && typeParameters.Any() ? $"<{string.Join(", ", typeParameters)}>" : "";
            Member($"{prefix}{name}{generics}({parameters}) {{", bodyAction, sourceNode);
        }

        public void Raw(string content, SyntaxNode? sourceNode = null) => _builder.Line(content, sourceNode);

        /// <summary>A member with a body, and the blank line every member is followed by.</summary>
        private void Member(string signature, Action bodyAction, SyntaxNode? sourceNode)
        {
            if (sourceNode != null) _builder.RecordMapping(sourceNode);
            using (_builder._writer.BeginBlock(signature)) bodyAction();
            _builder.Line("");
        }
    }
}
