using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using eQuantic.UI.Compiler.CodeGen;
using eQuantic.UI.Compiler.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Probe = eQuantic.UI.Conformance.Tests.Infrastructure.NullArgumentSurface.Probe;

namespace eQuantic.UI.Conformance.Tests.Infrastructure;

/// <summary>
/// Runs the null-argument probes on both sides, as one compilation and one program per side: a few
/// hundred probes, one process each, would be minutes of bun and Roslyn start-up for a measurement that
/// takes seconds. Each probe is a static method of one class; .NET compiles it as a library and calls
/// every method, and eqc's converter translates every body from the same compilation's model, so both
/// sides read the C# the same way.
/// <para>
/// Before either side runs, a probe must earn its place: it compiles, its call binds to the member it
/// names (an argument typed loosely binds a sibling overload, and the probe would measure that), and
/// eqc translates it without an error. One the converter refuses is a member the build refuses, which no
/// browser meets, so it is left out and counted rather than compared.
/// </para>
/// </summary>
internal static class NullArgumentRun
{
    private const string Cases = "NullArgumentProbes";

    /// <summary>What a call did: returned (and what, where the value is compared) or threw.</summary>
    /// <param name="Threw">Whether the call threw.</param>
    /// <param name="Type">The exception's .NET type, in full.</param>
    /// <param name="Parameter">Its <c>ParamName</c>.</param>
    /// <param name="Message">Its message, the host's newline folded to the SDK's.</param>
    /// <param name="Value">What the call returned, as JSON, where the probe compares it.</param>
    /// <param name="Composed">Whether the message is one the SDK composes. An engine's own TypeError,
    /// which the runtime reads as a NullReferenceException, words its message as the engine does.</param>
    internal sealed record Outcome(bool Threw, string? Type, string? Parameter, string? Message, string? Value, bool Composed)
    {
        public override string ToString() => Threw
            ? $"threw {Type} (ParamName {Parameter ?? "null"}): {Message}"
            : Value is null ? "returned" : $"returned {Value}";
    }

    /// <summary>A probe left out of the comparison, and why.</summary>
    internal sealed record Excluded(Probe Probe, string Why);

    /// <summary>A probe both sides ran, what eqc wrote for it, and its control where one ran too.</summary>
    internal sealed record Compared(Probe Probe, string Js, Outcome DotNet, Outcome JavaScript, Outcome? ControlDotNet, Outcome? ControlJavaScript);

    /// <summary>The probes compared and what each side did, and those left out.</summary>
    internal sealed record Measurement(IReadOnlyList<Compared> Compared, IReadOnlyList<Excluded> Refused, IReadOnlyList<Excluded> Invalid);

    /// <summary>One method of the probes' class: a probe's null call, or its control.</summary>
    private sealed record Case(int Probe, bool IsControl, string Statements, MethodBase Target, bool Keep);

    /// <summary>The framework, read once for every compilation (#481).</summary>
    private static readonly Lazy<MetadataReference[]> References = new(() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(file => file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(file => TestReferences.Of(file))
            .ToArray());

    internal static Measurement Measure(IReadOnlyList<Probe> probes)
    {
        var cases = probes.SelectMany((probe, i) => probe.Control is null
                ? new[] { new Case(i, false, probe.Statements, probe.Target, probe.ComparesValue) }
                : [new Case(i, false, probe.Statements, probe.Target, probe.ComparesValue),
                    new Case(i, true, probe.Control, probe.Target, probe.ComparesValue)])
            .ToList();

        // A case whose C# does not compile, or whose call binds elsewhere, says nothing about eqc.
        var (_, firstModel, firstMethods, firstErrors) = Compile(cases);
        var invalid = new Dictionary<Case, string>();
        for (var i = 0; i < cases.Count; i++)
        {
            if (firstErrors.TryGetValue(i, out var error)) invalid[cases[i]] = error;
            else if (BindingMismatch(firstModel, firstMethods[i], cases[i].Target) is { } elsewhere) invalid[cases[i]] = elsewhere;
        }

        var valid = cases.Where(c => !invalid.ContainsKey(c)).ToList();
        var (compilation, model, methods, errors) = Compile(valid);
        if (errors.Count > 0)
            throw new InvalidOperationException("the valid probes do not compile together: " + string.Join("; ", errors.Values));

        // eqc's verdict, case by case, from the compilation .NET runs.
        var translated = new List<(Case Case, int Index, string Js)>();
        var refused = new Dictionary<Case, string>();
        for (var i = 0; i < valid.Count; i++)
        {
            var converter = new CSharpToJsConverter { SymbolsAreAuthoritative = true };
            converter.SetSemanticModel(model);
            try
            {
                var js = converter.Convert(methods[i].Body!);
                var refusals = converter.Diagnostics.Where(d => d.Severity == ConversionSeverity.Error).ToList();
                if (refusals.Count > 0) refused[valid[i]] = string.Join("; ", refusals.Select(d => $"{d.Code} {d.Message}"));
                else translated.Add((valid[i], i, js));
            }
            catch (Exception exception)
            {
                refused[valid[i]] = $"the converter threw {exception.GetType().Name}: {exception.Message}";
            }
        }

        var dotNet = DotNetOutcomes(compilation, valid);
        var javaScript = JavaScriptOutcomes(translated.Select(t => (t.Js, t.Case.Keep)).ToList());
        var ran = translated.Select((t, k) => (t.Case, t.Js, DotNet: dotNet[t.Index], JavaScript: javaScript[k]))
            .ToDictionary(r => (r.Case.Probe, r.Case.IsControl));

        // A probe is what its null call is; a control that did not compile or that eqc refuses is no
        // control, and the probe is compared on its own.
        var compared = new List<Compared>();
        var refusedProbes = new List<Excluded>();
        var invalidProbes = new List<Excluded>();
        foreach (var @case in cases.Where(c => !c.IsControl))
        {
            var probe = probes[@case.Probe];
            if (invalid.TryGetValue(@case, out var why)) invalidProbes.Add(new Excluded(probe, why));
            else if (refused.TryGetValue(@case, out why)) refusedProbes.Add(new Excluded(probe, why));
            else
            {
                var run = ran[(@case.Probe, false)];
                ran.TryGetValue((@case.Probe, true), out var control);
                compared.Add(new Compared(probe, run.Js, run.DotNet, run.JavaScript, control.Case is null ? null : control.DotNet,
                    control.Case is null ? null : control.JavaScript));
            }
        }

        return new Measurement(compared, refusedProbes, invalidProbes);
    }

    // ---- One compilation ------------------------------------------------------------------------

    private static string Source(IReadOnlyList<Case> cases)
    {
        var source = new StringBuilder();
        foreach (var space in NullArgumentSurface.Namespaces) source.Append($"using {space};\n");
        source.Append($"\npublic static class {Cases}\n{{\n");
        for (var i = 0; i < cases.Count; i++)
            source.Append($"    public static object Case{i}()\n    {{\n        {cases[i].Statements}\n    }}\n");
        return source.Append("}\n").ToString();
    }

    /// <summary>The cases compiled as one library, each case's method at its own index (they are
    /// declared in order), and the errors each one's C# has.</summary>
    private static (CSharpCompilation Compilation, SemanticModel Model, IReadOnlyList<MethodDeclarationSyntax> Methods,
        Dictionary<int, string> Errors) Compile(IReadOnlyList<Case> cases)
    {
        var tree = CSharpSyntaxTree.ParseText(Source(cases), ParseDefaults.Options);
        var compilation = CSharpCompilation.Create("NullArgumentProbes", [tree], References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Every case is one method, so a diagnostic belongs to the method whose span holds it.
        var methods = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
        var errors = new Dictionary<int, string>();
        foreach (var diagnostic in compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
        {
            var index = methods.FindIndex(m => m.Span.Contains(diagnostic.Location.SourceSpan));
            if (index < 0) throw new InvalidOperationException($"a diagnostic outside every probe: {diagnostic}");
            errors.TryAdd(index, $"{diagnostic.Id} {diagnostic.GetMessage(CultureInfo.InvariantCulture)}");
        }

        return (compilation, compilation.GetSemanticModel(tree), methods, errors);
    }

    /// <summary>
    /// Why the probe's call does not bind to its member, or null when it does: the symbol the model
    /// binds and the member reflection named must be one metadata definition.
    /// </summary>
    private static string? BindingMismatch(SemanticModel model, MethodDeclarationSyntax method, MethodBase target)
    {
        var statements = method.Body!.Statements;
        ExpressionSyntax call = statements[^1] is ReturnStatementSyntax { Expression: not LiteralExpressionSyntax } answer
            ? answer.Expression!
            : ((ExpressionStatementSyntax)statements[^2]).Expression;
        if (call is AssignmentExpressionSyntax assignment) call = assignment.Left;

        var symbol = model.GetSymbolInfo(call).Symbol;
        var bound = symbol switch
        {
            IPropertySymbol indexer => target.Name == "set_Item" ? indexer.SetMethod : indexer.GetMethod,
            IMethodSymbol m => m.ReducedFrom ?? m,
            _ => null,
        };
        if (bound is null) return $"the call binds to no method ({symbol?.Kind.ToString() ?? "nothing"})";

        var definition = bound.OriginalDefinition;
        return definition.MetadataToken == target.MetadataToken
            && definition.ContainingAssembly.Name == target.Module.Assembly.GetName().Name
                ? null
                : $"the call binds to {bound.ToDisplayString()}";
    }

    // ---- .NET -----------------------------------------------------------------------------------

    /// <summary>
    /// The .NET side: the probes compiled as a library, loaded in a context of their own that is unloaded
    /// after, and each one called in the invariant culture, for its messages too.
    /// </summary>
    private static Outcome[] DotNetOutcomes(CSharpCompilation compilation, IReadOnlyList<Case> cases)
    {
        using var image = new MemoryStream();
        var emitted = compilation.Emit(image);
        if (!emitted.Success)
            throw new InvalidOperationException("the probes do not emit: " + string.Join("; ", emitted.Diagnostics));
        image.Position = 0;

        var context = new AssemblyLoadContext(Cases, isCollectible: true);
        var (culture, uiCulture) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            var type = context.LoadFromStream(image).GetType(Cases)!;
            return cases.Select((@case, i) =>
            {
                try
                {
                    var value = type.GetMethod($"Case{i}")!.Invoke(null, null);
                    return new Outcome(false, null, null, null, @case.Keep ? DotNetEvaluator.ToJson(value) : null, true);
                }
                catch (TargetInvocationException thrown) when (thrown.InnerException is { } exception)
                {
                    return new Outcome(true, exception.GetType().FullName, (exception as ArgumentException)?.ParamName,
                        exception.Message.Replace("\r\n", "\n"), null, true);
                }
            }).ToArray();
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
            context.Unload();
        }
    }

    // ---- JavaScript -----------------------------------------------------------------------------

    /// <summary>How many probes one bun process runs.</summary>
    private const int Chunk = 120;

    /// <summary>
    /// The browser's side: each translated body run under a try, and what it threw described by the
    /// types the runtime gave it, which are what a typed <c>catch</c> reads (<c>$eq.exceptions.is</c>).
    /// A chunk that does not run at all (one body that does not parse costs every probe beside it) is
    /// run again probe by probe, so the one that does not run is named and the rest are measured.
    /// </summary>
    private static Outcome[] JavaScriptOutcomes(IReadOnlyList<(string Js, bool Keep)> bodies)
    {
        var outcomes = new List<Outcome>();
        foreach (var chunk in bodies.Chunk(Chunk))
        {
            try
            {
                outcomes.AddRange(RunJs(chunk));
            }
            catch (InvalidOperationException)
            {
                foreach (var body in chunk)
                {
                    try
                    {
                        outcomes.AddRange(RunJs([body]));
                    }
                    catch (InvalidOperationException alone)
                    {
                        var line = alone.Message.Split('\n').FirstOrDefault(l => l.Contains("rror")) ?? alone.Message.Split('\n')[0];
                        outcomes.Add(new Outcome(true, $"did not run: {line.Trim()}", null, null, null, false));
                    }
                }
            }
        }

        return outcomes.ToArray();
    }

    /// <summary>
    /// What describes a thrown value: the most derived of the .NET types the runtime tagged it with
    /// (the chain <c>$eq.exceptions.is</c> reads, kept under the runtime's registered symbol), or, for an
    /// error the runtime never tagged, the type <c>is</c> reads it as: an engine's TypeError is a
    /// NullReferenceException and anything else an Exception.
    /// </summary>
    internal const string Describe = """
        const __chain = Symbol.for('eq.exception.types');
        const __describe = (e) => {
          const chain = e !== null && typeof e === 'object' ? e[__chain] : undefined;
          if (chain !== undefined) return ['threw', chain[0], e.paramName ?? null, e.message, true];
          const type = $eq.exceptions.is(e, 'System.NullReferenceException') ? 'System.NullReferenceException'
            : $eq.exceptions.is(e, 'System.Exception') ? 'System.Exception' : 'not an exception: ' + String(e);
          return ['threw', type, null, e?.message ?? String(e), false];
        };
        const __run = (probe, keep) => {
          try {
            const value = probe();
            return keep ? ['returned', value === undefined ? null : value] : ['returned'];
          } catch (e) {
            return __describe(e);
          }
        };

        """;

    private static IEnumerable<Outcome> RunJs(IReadOnlyList<(string Js, bool Keep)> bodies)
    {
        var program = new StringBuilder(Describe);
        program.Append("console.log(JSON.stringify([\n");
        foreach (var (js, keep) in bodies) program.Append($"__run(() => {js}, {(keep ? "true" : "false")}),\n");
        program.Append("], (k, v) => typeof v === 'bigint' ? v.toString() : v));\n");

        var text = program.ToString();
        var json = JsExecutor.Run(ConformanceRunner.ImportOfWhatItNames(text) + text, timeoutMs: 60000);
        var answers = JsonSerializer.Deserialize<JsonElement[]>(json)
            ?? throw new InvalidOperationException($"bun printed no answers: {json}");
        if (answers.Length != bodies.Count)
            throw new InvalidOperationException($"bun answered {answers.Length} probes of {bodies.Count}");
        return answers.Select(Read);
    }

    private static Outcome Read(JsonElement answer)
    {
        if (answer[0].GetString() == "returned")
            return new Outcome(false, null, null, null, answer.GetArrayLength() > 1 ? answer[1].GetRawText() : null, true);
        return new Outcome(true, answer[1].GetString(),
            answer[2].ValueKind == JsonValueKind.Null ? null : answer[2].GetString(),
            answer[3].ValueKind == JsonValueKind.Null ? null : answer[3].GetString(),
            null, answer[4].GetBoolean());
    }
}
