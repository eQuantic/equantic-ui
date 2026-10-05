using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;
using Xunit.Abstractions;

namespace eQuantic.UI.Web.Tests;

/// <summary>TEMPORARY measurement for #552 — never committed.</summary>
public class ZzEnumBoxingMeasureTests(ITestOutputHelper output)
{
    private static string RepoRoot([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    [Fact]
    public void Measure()
    {
        var root = RepoRoot();
        var sets = new Dictionary<string, string[]>
        {
            ["Components+Charts"] = Directory.GetFiles(Path.Combine(root, "src", "eQuantic.UI.Components"), "*.cs")
                .Concat(Directory.GetFiles(Path.Combine(root, "src", "eQuantic.UI.Charts"), "*.cs")).ToArray(),
            ["Code engine"] = SharedComponentTranspilationTests.CodeEngineSources(root).ToArray(),
            ["Primitives Sheet+Forms"] = Directory.GetFiles(Path.Combine(root, "src", "eQuantic.UI.Primitives", "Sheet"), "*.cs")
                .Concat(Directory.GetFiles(Path.Combine(root, "src", "eQuantic.UI.Primitives", "Forms"), "*.cs")).ToArray(),
            ["Sample DefaultUIDashboard"] = Directory.GetFiles(Path.Combine(root, "samples", "DefaultUIDashboard"), "*.cs", SearchOption.AllDirectories)
                .Where(p => !p.Contains("/obj/") && !p.Contains("/bin/")).ToArray(),
        };
        string[] App(string dir) => Directory.Exists(dir)
            ? Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories).Where(p => !p.Contains("/obj/") && !p.Contains("/bin/")).ToArray()
            : [];
        var projects = Path.GetFullPath(Path.Combine(root, ".."));
        sets["equantic-web/src/eQuantic.Web (client)"] = App(Path.Combine(projects, "equantic-web", "src", "eQuantic.Web"));
        sets["equantic-web Domain+Application (server, context)"] = App(Path.Combine(projects, "equantic-web", "src", "eQuantic.Web.Domain"))
            .Concat(App(Path.Combine(projects, "equantic-web", "src", "eQuantic.Web.Application"))).ToArray();
        sets["equantic-code UI+Workbench+Docking (client)"] = App(Path.Combine(projects, "equantic-code", "src", "eQuantic.Code.UI"))
            .Concat(App(Path.Combine(projects, "equantic-code", "src", "eQuantic.Code.Workbench")))
            .Concat(App(Path.Combine(projects, "equantic-code", "src", "eQuantic.Code.Docking"))).ToArray();
        sets["equantic-code Core+Application (context)"] = App(Path.Combine(projects, "equantic-code", "src", "eQuantic.Code.Core"))
            .Concat(App(Path.Combine(projects, "equantic-code", "src", "eQuantic.Code.Application"))).ToArray();
        var selfCheck = Path.Combine(Path.GetTempPath(), "zz-selfcheck-enum-boxing.cs");
        File.WriteAllText(selfCheck, "public enum Zz { A, B } public static class ZzUse { public static string M(Zz z) { object o = z; var l = new System.Collections.Generic.List<object> { Zz.A }; return string.Format(\"{0}\", z) + o + l.Count; } }");
        sets["SELF-CHECK (expect 3)"] = [selfCheck];
        var allPaths = sets.Values.SelectMany(x => x).Distinct().ToList();
        var trees = allPaths.Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path)).ToList();
        trees.Add(CSharpSyntaxTree.ParseText(
            "global using System;\nglobal using System.Collections.Generic;\nglobal using System.Linq;\nglobal using System.Threading.Tasks;",
            path: "GlobalUsings.g.cs"));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)TestReferences.Of(path))
            .ToList();
        var compilation = CSharpCompilation.Create("Measure", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        static bool IsEnum(ITypeSymbol? t) =>
            t is INamedTypeSymbol { TypeKind: TypeKind.Enum }
            || t is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } n && n.TypeArguments[0].TypeKind == TypeKind.Enum;

        foreach (var (set, paths) in sets)
        {
            var boxing = new List<string>();
            var generic = new List<string>();
            var lines = 0;
            foreach (var tree in trees.Where(t => paths.Contains(t.FilePath)))
            {
                lines += tree.GetText().Lines.Count;
                var model = compilation.GetSemanticModel(tree);
                foreach (var node in tree.GetRoot().DescendantNodes())
                {
                    var op = model.GetOperation(node);
                    var candidates = new List<IConversionOperation>();
                    if (op is IConversionOperation explicitConv) candidates.Add(explicitConv);
                    if (op?.Parent is IConversionOperation { IsImplicit: true } implicitConv && ReferenceEquals(implicitConv.Operand, op)) candidates.Add(implicitConv);
                    foreach (var conv in candidates)
                    if (IsEnum(conv.Operand.Type)
                        && conv.Type is { } to && (to.IsReferenceType || to.SpecialType == SpecialType.System_ValueType || to.TypeKind == TypeKind.TypeParameter))
                    {
                        var at = tree.GetLineSpan(node.Span).StartLinePosition.Line + 1;
                        var parent = conv.Parent?.Kind.ToString() ?? "-";
                        boxing.Add($"{Path.GetFileName(tree.FilePath)}:{at} {conv.Operand.Type!.Name} -> {to.ToDisplayString()} ({parent}) `{Trim(node.Parent?.ToString() ?? node.ToString())}`");
                    }
                    if (op is IInvocationOperation { TargetMethod: { } m } && (m.TypeArguments.Any(IsEnum) || m.ContainingType.TypeArguments.Any(IsEnum)))
                        generic.Add($"{Path.GetFileName(tree.FilePath)}:{tree.GetLineSpan(node.Span).StartLinePosition.Line + 1} {m.ContainingType.Name}.{m.Name}<{string.Join(",", m.TypeArguments.Concat(m.ContainingType.TypeArguments).Select(a => a.Name))}>");
                    if (op is IObjectCreationOperation { Type: INamedTypeSymbol created } && created.TypeArguments.Any(IsEnum))
                        generic.Add($"{Path.GetFileName(tree.FilePath)}:{tree.GetLineSpan(node.Span).StartLinePosition.Line + 1} new {created.ToDisplayString()}");
                }
            }
            output.WriteLine($"== {set}: {paths.Length} files, {lines} lines; enum boxing conversions: {boxing.Distinct().Count()}; generic uses over an enum: {generic.Distinct().Count()}");
            foreach (var b in boxing.Distinct()) output.WriteLine("  BOX " + b);
            foreach (var g in generic.Distinct().Take(400)) output.WriteLine("  GEN " + g);
        }
    }

    private static string Trim(string s)
    {
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ");
        return s.Length > 110 ? s[..107] + "..." : s;
    }
}
