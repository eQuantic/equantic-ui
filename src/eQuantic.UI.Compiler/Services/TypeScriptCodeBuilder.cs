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

    /// <summary>The callback form: the class's members are collected into a <see cref="JsClass"/>
    /// and written through the one class writer below.</summary>
    public void Class(string name, string? baseClass, Action<ClassBuilder> buildAction, IEnumerable<string>? typeParameters = null, SyntaxNode? sourceNode = null, bool export = true, bool isAbstract = false)
    {
        var members = new ClassBuilder(this);
        buildAction(members);
        Write(new JsClass(name, baseClass, typeParameters?.ToList() ?? [], export, isAbstract, members.Members,
            new JsOrigin(sourceNode)));
    }

    /// <summary>
    /// The single writer of a class. The header, then every member through the member writer under
    /// the ONE layout rule — a blank line before a member with a body and before a field that
    /// follows one, fields contiguous, nothing after the last — then the closing brace and the
    /// blank line that separates classes.
    /// </summary>
    public void Write(JsClass jsClass)
    {
        if (jsClass.Origin?.Member is { } classNode) RecordMapping(classNode);
        var generics = jsClass.TypeParameters.Count > 0 ? $"<{string.Join(", ", jsClass.TypeParameters)}>" : "";
        var extendsClause = string.IsNullOrEmpty(jsClass.Base) ? "" : $" extends {jsClass.Base}";
        // `abstract` is emitted, not erased: a base that declares a member for its subclasses to
        // supply has to say so, or TypeScript reads the declaration as a real property and refuses
        // the accessor that implements it. In PLAIN JavaScript there is no such keyword — `abstract
        // class X {` is a parse error that costs the whole module — and nothing is lost by dropping
        // it, since the only thing it buys is a compile-time refusal to instantiate.
        var abstractKeyword = jsClass.Abstract && TypeAnnotations ? "abstract " : "";
        // The closing brace travels with the scope: a class body that opens and never closes is the
        // one bug generated code reliably ships, and a `using` makes it unrepresentable.
        using (_writer.BeginBlock($"{(jsClass.Export ? "export " : "")}{abstractKeyword}class {jsClass.Name}{generics}{extendsClause} {{"))
        {
            var written = 0;
            var lastHadBody = false;
            foreach (var member in jsClass.Members)
            {
                if (written > 0 && (member.HasBody || lastHadBody)) Write("");
                written++;
                lastHadBody = member.HasBody;
                // The body's source maps to the line it starts on, one level in.
                if (member.Origin?.Body is { } body) RecordMapping(body, member.Origin.BodyLine, 1);
                Line(JsMemberWriter.Write(member, Layout), member.Origin?.Member);
            }
        }
        Write("");
    }

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

    /// <summary>Collects a class's members in order; the layout is the writer's.</summary>
    public class ClassBuilder(TypeScriptCodeBuilder builder)
    {
        private readonly TypeScriptCodeBuilder _builder = builder;
        private readonly List<JsClassMember> _members = new();

        public IReadOnlyList<JsClassMember> Members => _members;

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
            var prefix = (isDeclare ? "declare " : "") + (isStatic ? "static " : "");
            var annotation = !_builder.TypeAnnotations || string.IsNullOrEmpty(type) ? "" : $": {type}";
            _members.Add(JsClassMember.Field(prefix, name, annotation, defaultValue) with { Origin = new JsOrigin(sourceNode) });
        }

        public void Property(string name, string type, bool isPublic = true, SyntaxNode? sourceNode = null)
        {
            if (!_builder.TypeAnnotations) return;   // a bare interface-style property is TypeScript-only
            var access = isPublic ? "" : "private ";
            _members.Add(JsClassMember.Field(access, name, $": {type}") with { Origin = new JsOrigin(sourceNode) });
        }

        public void Raw(string content, SyntaxNode? sourceNode = null) =>
            _members.Add(JsClassMember.Raw(content) with { Origin = new JsOrigin(sourceNode) });

        /// <summary>A member, with where it came from for the source map.</summary>
        public void Member(JsClassMember member, SyntaxNode? sourceNode = null,
            SyntaxNode? bodySource = null, int bodyLine = 1) =>
            _members.Add(member with { Origin = new JsOrigin(sourceNode, bodySource, bodyLine) });
    }
}
