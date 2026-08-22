namespace eQuantic.UI.Compiler.CodeGen.Ir;

/// <summary>One <c>import { names } from "from";</c>. The names are whatever the emitter decided the
/// module needs; ordering them is the writer's.</summary>
public sealed record JsImport(IReadOnlyList<string> Names, string From);

/// <summary>
/// A generated module: its imports, then its body. The body is still text — the classes the
/// builder lays out — and the imports are DATA, which is the point: what a module needs from the
/// runtime and from its siblings is the emitter's hardest decision, and a decision that comes out
/// as records can be asserted without parsing the file it ends up in.
/// </summary>
public sealed record JsModule(IReadOnlyList<JsImport> Imports, string Body);

/// <summary>The single writer of a module's text: the import lines, a blank line when there were
/// any, the body. An import with no names is not written — a line that imports nothing is noise.</summary>
public static class JsModuleWriter
{
    public static string Write(JsModule module)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var import in module.Imports)
        {
            if (import.Names.Count == 0) continue;
            builder.Append("import { ").Append(string.Join(", ", import.Names.OrderBy(name => name)))
                   .Append(" } from \"").Append(import.From).Append("\";\n");
        }
        if (builder.Length > 0) builder.Append('\n');
        return builder.Append(module.Body).ToString();
    }
}
