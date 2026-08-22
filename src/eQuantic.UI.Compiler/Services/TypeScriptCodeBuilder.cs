using eQuantic.UI.Codegen;
using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// The writer for the file type this compiler emits: TypeScript. Indentation, lines and blocks come
/// from the shared <see cref="CodeWriter"/> — what belongs HERE is what only a TypeScript file has:
/// imports, classes, members, and the source mapping that lets a browser show C#.
/// </summary>
public class TypeScriptCodeBuilder
{
    /// <summary>Emit TypeScript annotations (default) or plain JavaScript. Mirrors
    /// <c>TypeScriptEmitter.TypeAnnotations</c> — the emitter forwards its mode here.</summary>
    public bool TypeAnnotations { get; set; } = true;

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

    public void Class(string name, string? baseClass, Action<ClassBuilder> buildAction, IEnumerable<string>? typeParameters = null, SyntaxNode? sourceNode = null, bool export = true, bool isAbstract = false)
    {
        if (sourceNode != null) RecordMapping(sourceNode);
        var generics = typeParameters != null && typeParameters.Any() ? $"<{string.Join(", ", typeParameters)}>" : "";
        var extendsClause = string.IsNullOrEmpty(baseClass) ? "" : $" extends {baseClass}";
        // `abstract` is emitted, not erased: a base that declares a member for its subclasses to
        // supply has to say so, or TypeScript reads the declaration as a real property and refuses
        // the accessor that implements it. In PLAIN JavaScript there is no such keyword — `abstract
        // class X {` is a parse error that costs the whole module — and nothing is lost by dropping
        // it, since the only thing it buys is a compile-time refusal to instantiate.
        var abstractKeyword = isAbstract && TypeAnnotations ? "abstract " : "";

        // The closing brace travels with the scope: a class body that opens and never closes is the
        // one bug generated code reliably ships, and a `using` makes it unrepresentable.
        using (_writer.BeginBlock($"{(export ? "export " : "")}{abstractKeyword}class {name}{generics}{extendsClause} {{"))
        {
            buildAction(new ClassBuilder(this));
        }
        Write("");
    }

    /// <summary>How member bodies are laid out — the converter's layout, handed over by the emitter.</summary>
    public JsLayout Layout { get; set; } = JsLayout.Pretty;

    public void Indent() => _writer.IndentLevel++;

    public void Dedent() => _writer.IndentLevel = Math.Max(0, _writer.IndentLevel - 1);

    /// <summary>A line, optionally carrying the C# it came from into the source map.</summary>
    public void Line(string line, SyntaxNode? sourceNode = null)
    {
        if (sourceNode != null) RecordMapping(sourceNode);
        Write(line);
    }

    /// <summary>Every line of the content takes the current indentation — a member body arrives
    /// laid out in lines of its own. An EMPTY line carries none: trailing whitespace is a diff
    /// nobody wants.</summary>
    private void Write(string content)
    {
        foreach (var line in content.Split('\n'))
        {
            if (string.IsNullOrEmpty(line)) _writer.AppendLine();
            else _writer.AppendLine(line);
        }
    }

    /// <param name="lineOffset">Lines BELOW the current one the mapping points at — a member
    /// body's first statement, written as part of the member's own text.</param>
    /// <param name="indentOffset">Levels deeper than the current indentation that line sits at.</param>
    private void RecordMapping(SyntaxNode node, int lineOffset = 0, int indentOffset = 0)
    {
        var pos = node.GetLocation().GetLineSpan();
        _mappings.Add(new SourceMapping
        {
            GeneratedLine = _writer.CurrentLine + lineOffset,
            // 0-based column where the emitted line's content begins (after indentation).
            GeneratedColumn = (_writer.IndentLevel + indentOffset) * 4,
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
            // Plain-JavaScript mode: `declare` does not exist, and a declare field carries no
            // initializer — the whole line has nothing to say.
            if (!_builder.TypeAnnotations && isDeclare) return;
            var init = defaultValue != null ? $" = {defaultValue}" : "";
            var prefix = (isDeclare ? "declare " : "") + (isStatic ? "static " : "");
            var annotation = !_builder.TypeAnnotations || string.IsNullOrEmpty(type) ? "" : $": {type}";
            _builder.Line($"{prefix}{name}{annotation}{init};", sourceNode);
        }

        public void Property(string name, string type, bool isPublic = true, SyntaxNode? sourceNode = null)
        {
            if (!_builder.TypeAnnotations) return;   // a bare interface-style property is TypeScript-only
            var access = isPublic ? "" : "private ";
            _builder.Line($"{access}{name}: {type};", sourceNode);
        }

        public void Raw(string content, SyntaxNode? sourceNode = null) => _builder.Line(content, sourceNode);

        /// <summary>A member through the member writer. <paramref name="separated"/> adds the blank
        /// line the method family has always been followed by (the accessor family has not — the
        /// layout is the emitter's to normalize, not this seam's).</summary>
        public void Member(JsClassMember member, SyntaxNode? sourceNode = null, bool separated = false,
            SyntaxNode? bodySource = null, int bodyLine = 1)
        {
            // The body's source maps to the line it starts on — `bodyLine` lines below the signature
            // (past any statements the emitter put in front of it), one level in.
            if (bodySource != null) _builder.RecordMapping(bodySource, bodyLine, 1);
            _builder.Line(JsMemberWriter.Write(member, _builder.Layout), sourceNode);
            if (separated) _builder.Line("");
        }

    }
}
