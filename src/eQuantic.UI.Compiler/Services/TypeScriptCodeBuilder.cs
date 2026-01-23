using System.Text;

namespace eQuantic.UI.Compiler.Services;

public class TypeScriptCodeBuilder
{
    private readonly StringBuilder _sb = new();
    private int _indentLevel = 0;
    private const string IndentString = "    ";

    public void Import(IEnumerable<string> items, string from)
    {
        if (!items.Any()) return;
        var sortedItems = items.OrderBy(i => i);
        AppendLine($"import {{ {string.Join(", ", sortedItems)} }} from \"{from}\";");
    }

    public void Class(string name, string? baseClass, Action<ClassBuilder> buildAction)
    {
        var extendsClause = string.IsNullOrEmpty(baseClass) ? "" : $" extends {baseClass}";
        AppendLine($"export class {name}{extendsClause} {{");
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
    public void Line(string line) => AppendLine(line);

    public override string ToString() => _sb.ToString();

    public class ClassBuilder
    {
        private readonly TypeScriptCodeBuilder _builder;

        public ClassBuilder(TypeScriptCodeBuilder builder)
        {
            _builder = builder;
        }

        public void Field(string name, string type, string? defaultValue = null)
        {
            var init = defaultValue != null ? $" = {defaultValue}" : "";
            _builder.Line($"{name}: {type}{init};");
        }

        public void Property(string name, string type, bool isPublic = true)
        {
            var access = isPublic ? "" : "private ";
            _builder.Line($"{access}{name}: {type};");
        }

        public void Constructor(string parameters, Action bodyAction)
        {
            _builder.Line($"constructor({parameters}) {{");
            _builder.Indent();
            bodyAction();
            _builder.Dedent();
            _builder.Line("}");
            _builder.Line("");
        }
        
        public void Method(string name, string parameters, bool isAsync, Action bodyAction)
        {
            var prefix = isAsync ? "async " : "";
            _builder.Line($"{prefix}{name}({parameters}) {{");
            _builder.Indent();
            bodyAction();
            _builder.Dedent();
            _builder.Line("}");
            _builder.Line("");
        }

        public void Raw(string content) => _builder.Line(content);
    }
}
