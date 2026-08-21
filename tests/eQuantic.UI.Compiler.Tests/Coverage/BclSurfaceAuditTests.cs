using System.Reflection;
using System.Text;
using eQuantic.UI.Compiler;
using eQuantic.UI.Compiler.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Coverage;

/// <summary>
/// The BCL half of the coverage denominator: for every public member of the surface a CLIENT
/// component may reasonably touch, a minimal probe is transpiled under an AUTHORITATIVE model and
/// the verdict recorded — <c>native</c> (a strategy mapped it to plain JS), <c>eq</c> (routed
/// through a $eq compat helper), <c>fenced:EQxxxx</c> (refused with a diagnostic), or
/// <c>invalid-probe</c> (the generated snippet itself doesn't compile — signature outside the
/// probe generator's reach, not evidence about coverage).
/// <para>
/// The verdicts live in a COMMITTED baseline, so any change — a member gained, a mapping lost —
/// arrives as a reviewable diff instead of a discovery in someone's browser. The semantic
/// hardening is what makes "no diagnostic" trustworthy here: an unbound or untranslatable call
/// can no longer pass silently, so a clean transpile means a strategy genuinely answered.
/// Regenerate with EQ_UPDATE_BCL_BASELINE=1.
/// </para>
/// </summary>
public class BclSurfaceAuditTests
{
    // ---- The audited surface: closed instantiations a component actually uses -------------------

    private sealed record Receiver(Type Type, string Declaration, string Expression);

    private static readonly Receiver[] InstanceReceivers =
    [
        new(typeof(string), "string recvString = \"ab\";", "recvString"),
        new(typeof(int), "int recvInt = 3;", "recvInt"),
        new(typeof(double), "double recvDouble = 1.5;", "recvDouble"),
        new(typeof(bool), "bool recvBool = true;", "recvBool"),
        new(typeof(char), "char recvChar = 'a';", "recvChar"),
        new(typeof(long), "long recvLong = 3L;", "recvLong"),
        new(typeof(decimal), "decimal recvDecimal = 1.5m;", "recvDecimal"),
        new(typeof(List<int>), "List<int> recvList = new() { 1, 2 };", "recvList"),
        new(typeof(Dictionary<string, int>), "Dictionary<string, int> recvDict = new() { [\"a\"] = 1 };", "recvDict"),
        new(typeof(HashSet<int>), "HashSet<int> recvSet = new() { 1 };", "recvSet"),
        new(typeof(Queue<int>), "Queue<int> recvQueue = new();", "recvQueue"),
        new(typeof(Stack<int>), "Stack<int> recvStack = new();", "recvStack"),
        new(typeof(SortedSet<int>), "SortedSet<int> recvSortedSet = new();", "recvSortedSet"),
        new(typeof(SortedDictionary<string, int>), "SortedDictionary<string, int> recvSortedDict = new();", "recvSortedDict"),
        new(typeof(StringBuilder), "StringBuilder recvSb = new();", "recvSb"),
        new(typeof(Guid), "Guid recvGuid = Guid.NewGuid();", "recvGuid"),
        new(typeof(DateTime), "DateTime recvDateTime = new DateTime(2026, 1, 2);", "recvDateTime"),
        new(typeof(TimeSpan), "TimeSpan recvTimeSpan = TimeSpan.FromMinutes(90);", "recvTimeSpan"),
        new(typeof(DateOnly), "DateOnly recvDateOnly = new DateOnly(2026, 1, 2);", "recvDateOnly"),
        new(typeof(TimeOnly), "TimeOnly recvTimeOnly = new TimeOnly(10, 30);", "recvTimeOnly"),
        new(typeof(DateTimeOffset), "DateTimeOffset recvDto = new DateTimeOffset(new DateTime(2026, 1, 2), TimeSpan.Zero);", "recvDto"),
        new(typeof(int?), "int? recvNullable = 5;", "recvNullable"),
        new(typeof(int[]), "int[] recvArray = { 1, 2 };", "recvArray"),
    ];

    private static readonly Type[] StaticSurfaces =
    [
        typeof(Math), typeof(MathF), typeof(Convert), typeof(Guid), typeof(DateTime),
        typeof(TimeSpan), typeof(DateOnly), typeof(TimeOnly), typeof(string), typeof(int),
        typeof(double), typeof(bool), typeof(char), typeof(long), typeof(Array),
    ];

    /// <summary>LINQ over a materialized list — the shape client code overwhelmingly uses.</summary>
    private static readonly Type LinqSurface = typeof(System.Linq.Enumerable);

    // ---- Canonical argument values per parameter type -------------------------------------------

    private static string? ArgumentFor(Type parameter, Type? receiverElement)
    {
        if (parameter.IsByRef) return null; // out/ref probes stay out of the audit's v1 reach
        if (parameter.IsGenericParameter) return receiverElement == typeof(int) ? "1" : null;

        var type = Nullable.GetUnderlyingType(parameter) ?? parameter;
        if (type == typeof(int) || type == typeof(short) || type == typeof(byte) || type == typeof(uint)) return "1";
        if (type == typeof(long)) return "1L";
        if (type == typeof(double)) return "1.5";
        if (type == typeof(float)) return "1.5f";
        if (type == typeof(decimal)) return "1.5m";
        if (type == typeof(bool)) return "true";
        if (type == typeof(char)) return "'a'";
        if (type == typeof(string)) return "\"a\"";
        if (type == typeof(object)) return "\"a\"";
        if (type == typeof(StringComparison)) return "StringComparison.Ordinal";
        if (type == typeof(MidpointRounding)) return "MidpointRounding.ToEven";
        if (type == typeof(Guid)) return "Guid.NewGuid()";
        if (type == typeof(DateTime)) return "new DateTime(2026, 1, 2)";
        if (type == typeof(TimeSpan)) return "TimeSpan.FromMinutes(1)";
        if (type == typeof(DateOnly)) return "new DateOnly(2026, 1, 2)";
        if (type == typeof(TimeOnly)) return "new TimeOnly(10, 30)";
        if (type == typeof(int[])) return "new[] { 1 }";
        if (type == typeof(string[])) return "new[] { \"a\" }";
        if (type == typeof(char[])) return "new[] { 'a' }";
        if (type == typeof(IEnumerable<int>) || type == typeof(List<int>)) return "new List<int> { 1 }";
        if (type == typeof(IEnumerable<string>)) return "new List<string> { \"a\" }";

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            var args = type.GetGenericArguments();
            if (definition == typeof(Func<,>) && args.All(CanSpeak))
                return $"__x => {ArgumentFor(args[1], receiverElement) ?? "default"}";
            if (definition == typeof(Func<,,>) && args.All(CanSpeak))
                return $"(__x, __y) => {ArgumentFor(args[2], receiverElement) ?? "default"}";
            if (definition == typeof(Action<>) && CanSpeak(args[0]))
                return "__x => { }";
            if (definition == typeof(IEnumerable<>) && args[0].IsGenericParameter && receiverElement == typeof(int))
                return "new List<int> { 1 }";
        }

        return null;
    }

    private static bool CanSpeak(Type type) =>
        type.IsGenericParameter
        || type == typeof(int) || type == typeof(bool) || type == typeof(string)
        || type == typeof(double) || type == typeof(char) || type == typeof(long)
        || type == typeof(decimal) || type == typeof(object);

    // ---- Probe generation -----------------------------------------------------------------------

    private sealed record Probe(string Id, string Statement);

    private static bool Skip(MemberInfo member) =>
        member.GetCustomAttribute<ObsoleteAttribute>() is not null
        || member.Name.StartsWith("op_", StringComparison.Ordinal)
        || member.Name is "GetHashCode" or "GetType" or "GetTypeCode" or "Deconstruct"
            or "GetEnumerator" or "CopyTo" or "TryFormat" or "GetPinnableReference";

    private static bool Speakable(Type type) =>
        !type.IsByRef && !type.IsPointer && !type.IsByRefLike
        && type != typeof(IntPtr) && type != typeof(UIntPtr);

    private static IEnumerable<Probe> InstanceProbes(Receiver receiver)
    {
        var element = receiver.Type.IsGenericType ? receiver.Type.GetGenericArguments().FirstOrDefault() : null;

        foreach (var property in receiver.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => !Skip(p) && p.GetIndexParameters().Length == 0 && p.CanRead && Speakable(p.PropertyType))
                     .OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            yield return new Probe($"{Label(receiver.Type)}.{property.Name}",
                $"var __r = {receiver.Expression}.{property.Name};");
        }

        foreach (var method in receiver.Type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                     .Where(m => !Skip(m) && !m.IsSpecialName && !m.IsGenericMethodDefinition && Speakable(m.ReturnType))
                     .OrderBy(m => m.Name, StringComparer.Ordinal).ThenBy(m => m.GetParameters().Length))
        {
            if (BuildCall(receiver.Expression, method, element) is { } call)
                yield return new Probe($"{Label(receiver.Type)}.{Signature(method)}", call);
        }
    }

    private static IEnumerable<Probe> StaticProbes(Type surface)
    {
        foreach (var property in surface.GetProperties(BindingFlags.Public | BindingFlags.Static)
                     .Where(p => !Skip(p) && p.CanRead && Speakable(p.PropertyType))
                     .OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            yield return new Probe($"{surface.Name}.{property.Name}",
                $"var __r = {SpeakType(surface)}.{property.Name};");
        }

        foreach (var field in surface.GetFields(BindingFlags.Public | BindingFlags.Static)
                     .Where(f => !Skip(f) && Speakable(f.FieldType))
                     .OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            yield return new Probe($"{surface.Name}.{field.Name}",
                $"var __r = {SpeakType(surface)}.{field.Name};");
        }

        foreach (var method in surface.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                     .Where(m => !Skip(m) && !m.IsSpecialName && !m.IsGenericMethodDefinition
                         && Speakable(m.ReturnType) && m.ReturnType != typeof(void))
                     .OrderBy(m => m.Name, StringComparer.Ordinal).ThenBy(m => m.GetParameters().Length)
                     .GroupBy(m => m.Name).Select(g => g.First()))
        {
            if (BuildCall(SpeakType(surface), method, null) is { } call)
                yield return new Probe($"{surface.Name}.{Signature(method)}", call);
        }
    }

    private static IEnumerable<Probe> LinqProbes()
    {
        foreach (var method in LinqSurface.GetMethods(BindingFlags.Public | BindingFlags.Static)
                     .Where(m => !Skip(m) && m.GetParameters().Length is >= 1 and <= 3)
                     .OrderBy(m => m.Name, StringComparer.Ordinal).ThenBy(m => m.GetParameters().Length)
                     .GroupBy(m => (m.Name, m.GetParameters().Length)).Select(g => g.First()))
        {
            var parameters = method.GetParameters().Skip(1).ToArray();
            var args = parameters.Select(p => ArgumentFor(p.ParameterType, typeof(int))).ToArray();
            if (args.Any(a => a is null)) continue;

            var receiverIsInts = method.GetParameters()[0].ParameterType.Name.Contains("IEnumerable");
            if (!receiverIsInts) continue;

            yield return new Probe($"Enumerable.{method.Name}/{parameters.Length}",
                $"var __r = recvList.{method.Name}({string.Join(", ", args)});");
        }
    }

    private static string? BuildCall(string receiver, MethodInfo method, Type? element)
    {
        var args = method.GetParameters()
            .Select(p => p.IsOptional && ArgumentFor(p.ParameterType, element) is null
                ? "SKIP_OPTIONAL"
                : ArgumentFor(p.ParameterType, element))
            .ToList();
        while (args.Count > 0 && args[^1] == "SKIP_OPTIONAL") args.RemoveAt(args.Count - 1);
        if (args.Any(a => a is null or "SKIP_OPTIONAL")) return null;

        var call = $"{receiver}.{method.Name}({string.Join(", ", args)})";
        return method.ReturnType == typeof(void) ? call + ";" : $"var __r = {call};";
    }

    private static string Label(Type type) => type switch
    {
        _ when type == typeof(int?) => "Nullable<int>",
        _ when type == typeof(int[]) => "int[]",
        _ when type.IsGenericType =>
            $"{type.Name[..type.Name.IndexOf('`')]}<{string.Join(",", type.GetGenericArguments().Select(a => a.Name))}>",
        _ => type.Name,
    };

    private static string SpeakType(Type type) => type switch
    {
        _ when type == typeof(string) => "string",
        _ when type == typeof(int) => "int",
        _ when type == typeof(double) => "double",
        _ when type == typeof(bool) => "bool",
        _ when type == typeof(char) => "char",
        _ when type == typeof(long) => "long",
        _ => type.Name,
    };

    private static string Signature(MethodInfo method) =>
        $"{method.Name}({string.Join(",", method.GetParameters().Select(p => Label(Simplify(p.ParameterType))))})";

    private static Type Simplify(Type type) => Nullable.GetUnderlyingType(type) ?? type;

    // ---- The audit ------------------------------------------------------------------------------

    private const int ChunkSize = 40;

    [Fact]
    public void TheBclSurfaceVerdicts_MatchTheCommittedBaseline()
    {
        var probes = InstanceReceivers.SelectMany(InstanceProbes)
            .Concat(StaticSurfaces.SelectMany(StaticProbes))
            .Concat(LinqProbes())
            .GroupBy(p => p.Id).Select(g => g.First())
            .OrderBy(p => p.Id, StringComparer.Ordinal)
            .ToArray();

        var verdicts = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var chunk in probes.Chunk(ChunkSize))
            AuditChunk(chunk, verdicts);

        var report = new StringBuilder();
        report.AppendLine("# eqc BCL surface audit — the coverage DENOMINATOR, one verdict per probed member.");
        report.AppendLine("# native = a strategy mapped it to plain JS · eq = routed through a $eq compat helper");
        report.AppendLine("# fenced:EQxxxx = refused with that diagnostic · invalid-probe = the probe itself doesn't compile");
        report.AppendLine($"# totals: {string.Join(" · ", verdicts.Values.GroupBy(v => v.Split(':')[0]).OrderBy(g => g.Key).Select(g => $"{g.Key}={g.Count()}"))}");
        foreach (var (id, verdict) in verdicts)
            report.AppendLine($"{verdict,-14} {id}");

        var baselinePath = Path.Combine(RepoRoot(), "tests", "eQuantic.UI.Compiler.Tests", "Coverage", "bcl-surface.baseline.txt");
        if (Environment.GetEnvironmentVariable("EQ_UPDATE_BCL_BASELINE") == "1")
        {
            File.WriteAllText(baselinePath, report.ToString());
            return;
        }

        Assert.True(File.Exists(baselinePath),
            "No committed baseline — run once with EQ_UPDATE_BCL_BASELINE=1 and commit the file.");
        var committed = File.ReadAllText(baselinePath).Replace("\r\n", "\n");
        var current = report.ToString().Replace("\r\n", "\n");
        if (committed != current)
        {
            var committedLines = committed.Split('\n').ToHashSet();
            var currentLines = current.Split('\n').ToHashSet();
            var gained = currentLines.Except(committedLines).Where(l => l.Length > 0 && !l.StartsWith('#')).ToArray();
            var lost = committedLines.Except(currentLines).Where(l => l.Length > 0 && !l.StartsWith('#')).ToArray();
            Assert.Fail("BCL surface verdicts moved — review and regenerate (EQ_UPDATE_BCL_BASELINE=1):\n"
                + "  now:      " + string.Join("\n  now:      ", gained.Take(20))
                + "\n  before:   " + string.Join("\n  before:   ", lost.Take(20)));
        }
    }

    private static void AuditChunk(Probe[] chunk, SortedDictionary<string, string> verdicts)
    {
        // Receivers are FIELDS, initialized once at class level: a per-probe local declaration
        // would put every receiver's own emission (decimal → $eq.num.dec, DateTime → $eq.time.*)
        // inside every probe body and poison the per-member $eq verdict.
        var body = new StringBuilder();
        foreach (var receiver in InstanceReceivers)
            body.AppendLine("    private " + receiver.Declaration);
        for (var i = 0; i < chunk.Length; i++)
        {
            body.AppendLine($"    public void __probe_{i}()");
            body.AppendLine("    {");
            body.AppendLine("        " + chunk[i].Statement);
            body.AppendLine("    }");
        }

        var source = "using System;\nusing System.Collections.Generic;\nusing System.Linq;\nusing System.Text;\n"
            + "using eQuantic.UI.Primitives;\n\n"
            + "public sealed class Probe : StatelessComponent\n{\n"
            + "    public override VisualNode Build(ComponentContext context) => new Box();\n"
            + body
            + "}\n";

        var tree = CSharpSyntaxTree.ParseText(source, ParseDefaults.Options, path: "Probe.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        var compilation = CSharpCompilation.Create("Probe", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        // A probe whose C# does not COMPILE says nothing about eqc — record it as such by line.
        var sourceLines = source.Split('\n');
        var probeOfLine = new int[sourceLines.Length + 2];
        for (int line = 0, current = -1; line < sourceLines.Length; line++)
        {
            var match = System.Text.RegularExpressions.Regex.Match(sourceLines[line], @"__probe_(\d+)\(\)");
            if (match.Success) current = int.Parse(match.Groups[1].Value);
            probeOfLine[line + 1] = current;
        }

        var invalid = new HashSet<int>();
        foreach (var diagnostic in compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
        {
            var line = diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1;
            if (line >= 1 && line < probeOfLine.Length && probeOfLine[line] >= 0)
                invalid.Add(probeOfLine[line]);
        }

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        var result = compiler.CompileSource(source, "Probe.cs").Single();

        var failing = new Dictionary<int, string>();
        foreach (var error in result.Errors.Where(e => e.Code.StartsWith("EQ", StringComparison.Ordinal)))
        {
            if (error.Line >= 1 && error.Line < probeOfLine.Length && probeOfLine[error.Line] >= 0)
                failing.TryAdd(probeOfLine[error.Line], error.Code);
        }

        // $eq detection per probe: the emitted method bodies appear in declaration order.
        var emitted = result.TypeScript;
        for (var i = 0; i < chunk.Length; i++)
        {
            string verdict;
            if (invalid.Contains(i)) verdict = "invalid-probe";
            else if (failing.TryGetValue(i, out var code)) verdict = $"fenced:{code}";
            else
            {
                var start = emitted.IndexOf($"__probe_{i}()", StringComparison.Ordinal);
                var end = emitted.IndexOf($"__probe_{i + 1}()", StringComparison.Ordinal);
                var slice = start < 0 ? "" : end < 0 ? emitted[start..] : emitted[start..end];
                verdict = slice.Contains("$eq.") ? "eq" : "native";
            }

            verdicts[chunk[i].Id] = verdict;
        }
    }

    private static string RepoRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
}
