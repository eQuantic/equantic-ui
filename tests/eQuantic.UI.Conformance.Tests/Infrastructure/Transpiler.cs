using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using eQuantic.UI.Compiler;
using eQuantic.UI.Compiler.CodeGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Conformance.Tests.Infrastructure;

/// <summary>
/// Transpiles a standalone C# expression (or statement block) to JavaScript using the real
/// CSharpToJsConverter, with a semantic model so type-aware strategies (integer division,
/// enums, etc.) behave exactly as in a real build.
/// <para>
/// And refuses what a build refuses (#907). Where the build refuses a construct, the converter still
/// writes text, its fallback, and this used to hand that text to the runner as if it had compiled:
/// #761 measured a query's <c>let</c> answering 3 in the browser, where a build refuses the query
/// (EQ2008) and nothing reaches a browser. So each entry answers a <see cref="Translation"/>, whose
/// text the runner reaches through <see cref="Translation.Accepted"/> only, with the build's refusals,
/// read where the build reads them:
/// </para>
/// <list type="bullet">
/// <item>the converter's diagnostics over the text the runner would run (the case's body, the
/// prelude's records), by the build's rule: an error refuses, a warning does not
/// (<see cref="Refusal.Of(IEnumerable{ConversionDiagnostic})"/>);</item>
/// <item>what a build runs BESIDE the converter, over the prelude's declarations, asked of the build
/// itself (<see cref="PreludeRefusals"/>), so none of it is encoded here a second time.</item>
/// </list>
/// <para>
/// Nothing beside the converter is asked of the case's body. A build would compile it as the method
/// of a plain class, which is the program written here, and runs nothing else over such a method: the
/// client boundary's semantic validator is a stateful component's (<c>SemanticValidator.Validate</c>
/// answers nothing for anything else), so reading it here would refuse what a build accepts.
/// </para>
/// </summary>
public static class Transpiler
{
    /// <summary>The usings every case reads, before its prelude.</summary>
    private const string Usings = """
        using System;
        using System.Linq;
        using System.Collections.Generic;
        using System.Text;
        using System.Threading.Tasks;
        """;

    /// <summary>What the model of every case and prelude reads, shared by all of them (#481).</summary>
    private static readonly MetadataReference[] References =
    [
        TestReferences.Of(typeof(object).Assembly.Location),
        TestReferences.Of(typeof(Enumerable).Assembly.Location),
        TestReferences.Of(typeof(System.Collections.Generic.List<>).Assembly.Location),
        TestReferences.Of(typeof(System.Collections.Generic.Stack<>).Assembly.Location),
        TestReferences.Of(typeof(System.DateOnly).Assembly.Location),
        TestReferences.Of(Assembly.Load("System.Runtime").Location),
        TestReferences.Of(Assembly.Load("System.Collections").Location),
        // The vocabulary, as the .NET side references it: a reference brings no name into
        // scope, so only a case that says `using eQuantic.UI.Primitives;` reads it.
        TestReferences.Of(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location),
    ];

    /// <summary>The build's verdict on each prelude, asked once: the cases of a class share a few
    /// preludes, and what the build says of one never changes.</summary>
    private static readonly ConcurrentDictionary<string, Lazy<IReadOnlyList<Refusal>>> PreludeVerdicts =
        new(StringComparer.Ordinal);

    /// <summary>
    /// ONE compiler asks every prelude, one prelude at a time, which is how a build uses one: every
    /// file of a project goes through the same instance. A compiler reads its standard references into
    /// native memory when it is made, measured at about 34 MB a compiler, with 1.2 GB of a hundred
    /// still held after they were released and collected, so one per prelude would pay that hundreds
    /// of times (#481).
    /// <para>
    /// What one prelude leaves in it decides nothing for the next, read in <c>ComponentCompiler</c> and
    /// what it holds. The project compilation is replaced, with the referenced trees beside it, and so
    /// is the dependency resolver, scanned from that compilation (the emitter's import net, the
    /// parser's chains and the converter's fallback receivers all read the new one). No referenced
    /// modules are ever handed over: a prelude references no project. The emitter resets the
    /// converter at every module (its diagnostics, its refused twins, the names it used, its node cache
    /// and its temporaries), and builds a new semantic helper with each model. What survives is keyed by
    /// identity (the static tables of <c>ValueCopies</c>, <c>LocalFunctionName</c>,
    /// <c>ResourceClasses</c> and <c>JsExprWriter</c>, keyed by a compilation, a node or an IR node),
    /// or is never read for an error (the resource reads and the discovered resource classes), or is
    /// keyed by name and answered by a type .NET loads, which no prelude's is
    /// (<c>ExceptionDefaultMessage</c>). <c>HarnessSelfChecksTests</c> asks two preludes that declare
    /// one type name differently in both orders.
    /// </para>
    /// </summary>
    private static readonly ComponentCompiler Build = new();

    private static readonly Lock BuildGate = new();

    /// <summary>
    /// Emits any positional records declared in the prelude as named JS classes (so instance methods,
    /// structural equality, etc. are available to the program under test). Returns "" when there are none.
    /// </summary>
    public static string EmitDeclaredRecordTypes(string prelude) => TranslateDeclaredRecordTypes(prelude).Accepted();

    public static string TranspileExpression(string csharpExpression, string prelude = "") =>
        TranslateExpression(csharpExpression, prelude).Accepted();

    /// <summary>
    /// Transpiles a block of C# statements (the body of <c>__Eval</c>) to a JS block <c>{ … }</c>.
    /// The block is expected to <c>return</c> a value; the runner wraps it in an IIFE to capture it.
    /// Exercises the control-flow statement strategies (if/for/foreach/while/switch/try/…).
    /// </summary>
    public static string TranspileStatements(string csharpStatements, string prelude = "") =>
        TranslateStatements(csharpStatements, prelude).Accepted();

    /// <summary>The prelude's records as <see cref="EmitDeclaredRecordTypes"/> writes them, with what
    /// the build refuses in the prelude.</summary>
    public static Translation TranslateDeclaredRecordTypes(string prelude)
    {
        if (string.IsNullOrWhiteSpace(prelude)) return new Translation(string.Empty, [], [], () => []);

        var program = Compile("return 0;", prelude);
        var emitter = new RecordTypeEmitter(program.Converter);
        var valueTypes = program.Tree.GetRoot()
            .DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            // The prelude is one tree, so its declarations say all there is to say.
            .Where(declaration => RecordTypeEmitter.CanEmit(declaration, symbol: null))
            .ToList();
        if (valueTypes.Count == 0) return program.Translated(string.Empty);

        // Emit base records before derived ones — JS `class X extends Base` needs Base already declared,
        // whether the base clause passes arguments (`: Animal(Name)`) or names it bare (`: Animal`).
        var names = valueTypes.Select(t => t.Identifier.Text).ToHashSet();
        string? EmittedBaseOf(TypeDeclarationSyntax t)
        {
            var b = t.BaseList?.Types.FirstOrDefault()?.Type.ToString();
            if (b != null && b.Contains('<')) b = b[..b.IndexOf('<')];
            return b != null && names.Contains(b) ? b : null;
        }

        var ordered = new List<TypeDeclarationSyntax>();
        var done = new HashSet<string>();
        void Add(TypeDeclarationSyntax t)
        {
            if (!done.Add(t.Identifier.Text)) return;
            var baseName = EmittedBaseOf(t);
            var baseType = baseName == null ? null : valueTypes.FirstOrDefault(x => x.Identifier.Text == baseName);
            if (baseType != null) Add(baseType);
            ordered.Add(t);
        }
        foreach (var t in valueTypes) Add(t);

        // Plain-JS emission (no TS `declare` type declarations) — this output runs as .mjs.
        return program.Translated(string.Join("\n", ordered.Select(t => emitter.Emit(t))) + "\n");
    }

    /// <summary>The expression as <see cref="TranspileExpression"/> writes it, with what the build
    /// refuses in it and in its prelude.</summary>
    public static Translation TranslateExpression(string csharpExpression, string prelude = "")
    {
        var program = Compile($"return {csharpExpression};", prelude);
        // Scope to the __Eval method body — a prelude type may now declare methods whose own `return`
        // statements would otherwise be picked up by a tree-wide First().
        var returnExpr = program.Tree.GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.Text == "__Eval")
            .Body!
            .DescendantNodes()
            .OfType<ReturnStatementSyntax>()
            .First()
            .Expression!;

        return program.Translated(program.Converter.ConvertExpression(returnExpr));
    }

    /// <summary>The block as <see cref="TranspileStatements"/> writes it, with what the build refuses
    /// in it and in its prelude.</summary>
    public static Translation TranslateStatements(string csharpStatements, string prelude = "")
    {
        var program = Compile(csharpStatements, prelude);
        var body = program.Tree.GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.Text == "__Eval")
            .Body!;

        // No declarations of its own: every statement declares what its expressions declare, with
        // C#'s scope (ExpressionVariableScanner). The harness once put a `let` for every `out var`
        // at the top of the block, which is what the emitter did for a method and nothing else did:
        // the cases passed while a getter, a loop and a deconstruction diverged.
        return program.Translated(program.Converter.Convert(body)); // dispatches to ConvertBlock -> "{ … }"
    }

    /// <summary>
    /// What a build refuses among the prelude's declarations, asked of the build: the prelude compiled
    /// as an app's file is compiled, one module per type, by the compiler eqc runs. So every check a
    /// build runs beside the converter on a declaration is read as the build reads it, and written
    /// nowhere here: two methods of one name (EQ1007), one name declared twice (EQ2009), a union's
    /// body (EQ2008), a component's own checks. The converter's refusals in each module come with it.
    /// </summary>
    private static IReadOnlyList<Refusal> PreludeRefusals(string prelude) =>
        string.IsNullOrWhiteSpace(prelude)
            ? []
            : PreludeVerdicts.GetOrAdd(prelude,
                static text => new Lazy<IReadOnlyList<Refusal>>(() => AskTheBuild(text))).Value;

    /// <summary>The build's verdict on <paramref name="prelude"/>, asked now and never cached: what
    /// <see cref="PreludeRefusals"/> remembers, and what the harness's own check asks in an order of its
    /// choosing, to show the verdict owes nothing to the prelude asked before it.</summary>
    internal static IReadOnlyList<Refusal> AskTheBuild(string prelude)
    {
        // A file of its own, compiled as the project's compilation holds it: eqc's tree replaces the
        // project's tree of the same path, as it does for every file of an app.
        const string path = "Prelude.cs";
        var source = $"{Usings}\n\n{prelude}\n";
        var tree = CSharpSyntaxTree.ParseText(source, eQuantic.UI.Compiler.Services.ParseDefaults.Options, path: path);
        var compilation = CSharpCompilation.Create("ConformancePrelude", [tree], References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        lock (BuildGate)
        {
            Build.SetProjectCompilation(compilation);
            // The scan eqc hands the compiler before it compiles a file of the app (AppBuild), here over
            // the one file there is: a module's import net asks it which types became modules, and
            // refuses a type whose owner never crosses (EQ2010) where no fence of the converter looks.
            Build.SetDependencyResolver(eQuantic.UI.Compiler.Services.ComponentDependencyResolver.From(compilation));
            return Refusal.Of(Build.CompileSource(source, path).ToList());
        }
    }

    private static Program Compile(string evalBody, string prelude)
    {
        var code = $@"
{Usings}

{prelude}

public class __Conformance
{{
    public async Task<object?> __Eval()
    {{
        {evalBody}
    }}
}}";

        // The SAME options eqc parses with in production. Roslyn's default is the latest
        // RELEASED version, so a harness on the default is not testing what ships: a
        // construct eqc accepts (C# 15's labeled jumps, `union`, `closed`, collection
        // `with(...)`) would fail to parse HERE and read as a translation bug.
        var tree = CSharpSyntaxTree.ParseText(code, eQuantic.UI.Compiler.Services.ParseDefaults.Options);
        var compilation = CSharpCompilation.Create(
            "ConformanceAsm",
            new[] { tree },
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // The conformance snippet is compiled with real references above, so the model is
        // AUTHORITATIVE: an unbindable call in a conformance case is a broken case, never a guess.
        var converter = new CSharpToJsConverter { SymbolsAreAuthoritative = true };
        converter.SetSemanticModel(compilation.GetSemanticModel(tree));
        return new Program(tree, compilation, converter, prelude);
    }

    /// <summary>One case's program: its tree, the compilation the model is read from, the converter
    /// that writes it, and the prelude whose declarations the build is asked about.</summary>
    private sealed record Program(SyntaxTree Tree, CSharpCompilation Compilation, CSharpToJsConverter Converter,
        string Prelude)
    {
        /// <summary>The text the converter wrote, with the converter's refusals over this text as its
        /// own, apart from the build's over the prelude.</summary>
        public Translation Translated(string js) => new(js,
            Refusal.Of(Converter.Diagnostics), PreludeRefusals(Prelude),
            () => Compilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Select(diagnostic => diagnostic.ToString())
                .ToList());
    }
}
