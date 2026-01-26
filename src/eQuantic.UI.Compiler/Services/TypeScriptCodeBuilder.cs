using System.Text;
using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.Services;

public class TypeScriptCodeBuilder
{
    private readonly StringBuilder _sb = new();
    private int _indentLevel = 0;
    private const string IndentString = "    ";

    // Source mapping data
    private int _currentLine = 1;
    private int _currentColumn = 1;
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
        AppendLine($"import {{ {string.Join(", ", sortedItems)} }} from \"{from}\";");
    }

    public void Class(string name, string? baseClass, Action<ClassBuilder> buildAction, IEnumerable<string>? typeParameters = null, SyntaxNode? sourceNode = null)
    {
        if (sourceNode != null) RecordMapping(sourceNode);
        var generics = typeParameters != null && typeParameters.Any() ? $"<{string.Join(", ", typeParameters)}>" : "";
        var extendsClause = string.IsNullOrEmpty(baseClass) ? "" : $" extends {baseClass}";
        AppendLine($"export class {name}{generics}{extendsClause} {{");
        Indent();
        buildAction(new ClassBuilder(this));
        Dedent();
        AppendLine("}");
        AppendLine();
    }

    private void AppendLine(string line = "")
    {
        if (!string.IsNullOrEmpty(line))
        {
            _sb.Append(string.Concat(Enumerable.Repeat(IndentString, _indentLevel)));
            _sb.AppendLine(line);
        }
        else
        {
            _sb.AppendLine();
        }
    }

    public void Indent() => _indentLevel++;
    public void Dedent() => _indentLevel = Math.Max(0, _indentLevel - 1);
    
    // Internal helper exposed via builder context
    public void Line(string line, SyntaxNode? sourceNode = null) 
    {
        if (sourceNode != null) RecordMapping(sourceNode);
        AppendLine(line);
    }

    private void RecordMapping(SyntaxNode node)
    {
        var pos = node.GetLocation().GetLineSpan();
        _mappings.Add(new SourceMapping
        {
            GeneratedLine = _currentLine,
            GeneratedColumn = _currentColumn,
            SourceLine = pos.StartLinePosition.Line + 1,
            SourceColumn = pos.StartLinePosition.Character + 1,
            SourceFile = pos.Path
        });
    }

    public override string ToString() => _sb.ToString();

    public class ClassBuilder
    {
        private readonly TypeScriptCodeBuilder _builder;

        public ClassBuilder(TypeScriptCodeBuilder builder)
        {
            _builder = builder;
        }

        public void Field(string name, string type, string? defaultValue = null, SyntaxNode? sourceNode = null)
        {
            var init = defaultValue != null ? $" = {defaultValue}" : "";
            _builder.Line($"{name}: {type}{init};", sourceNode);
        }

        public void Property(string name, string type, bool isPublic = true, SyntaxNode? sourceNode = null)
        {
            var access = isPublic ? "" : "private ";
            _builder.Line($"{access}{name}: {type};", sourceNode);
        }

        public void Constructor(string parameters, Action bodyAction, SyntaxNode? sourceNode = null)
        {
            _builder.Line($"constructor({parameters}) {{", sourceNode);
            _builder.Indent();
            bodyAction();
            _builder.Dedent();
            _builder.Line("}");
            _builder.Line("");
        }
        
        public void Method(string name, string parameters, bool isAsync, Action bodyAction, IEnumerable<string>? typeParameters = null, SyntaxNode? sourceNode = null)
        {
            var prefix = isAsync ? "async " : "";
            var generics = typeParameters != null && typeParameters.Any() ? $"<{string.Join(", ", typeParameters)}>" : "";
            _builder.Line($"{prefix}{name}{generics}({parameters}) {{", sourceNode);
            _builder.Indent();
            bodyAction();
            _builder.Dedent();
            _builder.Line("}");
            _builder.Line("");
        }

        public void Raw(string content, SyntaxNode? sourceNode = null) => _builder.Line(content, sourceNode);
    }
}
