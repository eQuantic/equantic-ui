using System.Text.Encodings.Web;
using System.Text.Json;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;
using Outcome = eQuantic.UI.Conformance.Tests.Infrastructure.NullArgumentRun.Outcome;
using Probe = eQuantic.UI.Conformance.Tests.Infrastructure.NullArgumentSurface.Probe;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A null handed to a BCL member eqc translates is answered as .NET answers it (#569). A .NET method
/// that cannot take a null refuses it with an <c>ArgumentNullException</c> naming the parameter; a twin
/// that reads the argument first throws the browser's <c>TypeError</c>, which the runtime reads as a
/// <c>NullReferenceException</c>, and since #561 a <c>catch (ArgumentNullException)</c> lets that
/// through. #561 fixed the seven its review named by hand. This is the measurement instead of the next
/// report: every member of the translated surface, called with null for each of its reference
/// parameters, on both sides, comparing what the call did.
/// <para>
/// What is compared, in this order, and the first that differs is the gap's aspect: whether the call
/// threw and the exception's type (<c>type</c>), its <c>ParamName</c> (<c>param</c>), its message
/// (<c>message</c>, the host's newline folded, and only for a message the SDK composes: an engine's
/// own TypeError words its message as the engine does), and, for a call that returned a string, a bool,
/// a char or a number, what it returned (<c>value</c>): a null .NET accepts is answered too.
/// </para>
/// <para>
/// The gaps left are a COMMITTED baseline, <c>null-argument-gaps.baseline.txt</c>, one line per probe
/// with its aspect and a reason, which may only shrink: a probe that diverges and is not listed fails,
/// an entry whose aspect moved fails, and an entry that now agrees fails until it is removed.
/// Regenerate with EQ_UPDATE_NULL_ARGUMENT_BASELINE=1 after a removal; it carries every reason across
/// and writes "not looked at yet" for a new entry, which is not an approval.
/// </para>
/// </summary>
public class NullArgumentConformanceTests(ITestOutputHelper output)
{
    private const string UpdateVariable = "EQ_UPDATE_NULL_ARGUMENT_BASELINE";

    /// <summary>What a fresh entry says until somebody decides: measured, not judged.</summary>
    private const string Unjudged = "not looked at yet";

    private sealed record Surface(
        IReadOnlyList<Probe> Probes,
        IReadOnlyList<NullArgumentSurface.Unspoken> Unspoken,
        IReadOnlyList<Type> Owners,
        IReadOnlyList<string> UnresolvedOwners);

    private static readonly Lazy<Surface> TheSurface = new(Derive);

    private static readonly Lazy<NullArgumentRun.Measurement> TheMeasurement =
        new(() => NullArgumentRun.Measure(TheSurface.Value.Probes));

    private static Surface Derive()
    {
        var (owners, unresolved) = NullArgumentSurface.Owners();
        var probes = new List<Probe>();
        var unspoken = new List<NullArgumentSurface.Unspoken>();
        foreach (var owner in owners)
        {
            foreach (var member in NullArgumentSurface.Members(owner))
            {
                var (some, why) = NullArgumentSurface.ProbesOf(owner, member);
                probes.AddRange(some);
                unspoken.AddRange(why);
            }
        }

        return new Surface(probes.OrderBy(p => p.Id, StringComparer.Ordinal).ToList(), unspoken, owners, unresolved);
    }

    // ---- The derivation -------------------------------------------------------------------------

    /// <summary>
    /// The surface comes whole from the audit's record: every type it names resolves, and every probe
    /// has an id of its own, the baseline's key.
    /// </summary>
    [Fact]
    public void TheSurface_IsEveryTypeTheAuditNames()
    {
        var surface = TheSurface.Value;
        Assert.True(surface.UnresolvedOwners.Count == 0,
            "The BCL audit's record names a type this derivation cannot resolve, so its members would drop "
            + "out of the null-argument theory without a word. Teach NullArgumentSurface.TypeOf the label:\n  "
            + string.Join("\n  ", surface.UnresolvedOwners));

        var shared = surface.Probes.GroupBy(p => p.Id, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(shared.Count == 0, "Two probes share an id, so the baseline cannot tell them apart:\n  " + string.Join("\n  ", shared));
        Assert.NotEmpty(surface.Probes);
    }

    /// <summary>
    /// Every member the audit says eqc translates (<c>native</c> or <c>eq</c>) that takes a reference
    /// parameter is called with a null and compared. The audit's line is the claim; a claim that reaches
    /// no comparison, because no value of an argument can be written, the call binds a sibling or eqc
    /// refuses the probe, fails here and says which. A LINQ line names an operator and its arity, and is
    /// reached when one overload of that arity is.
    /// </summary>
    [SkippableFact]
    public void EveryMemberTheAuditSaysEqcTranslates_IsCalledWithANull()
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        var surface = TheSurface.Value;
        var measurement = TheMeasurement.Value;
        var compared = measurement.Compared.Where(MeasuresTheNull).Select(c => c.Probe.Target).ToHashSet();
        var why = surface.Unspoken.Select(u => (u.MemberId, u.Why))
            .Concat(measurement.Refused.Concat(measurement.Invalid).Select(e => (e.Probe.MemberId, e.Why)))
            .Concat(measurement.Compared.Where(c => !MeasuresTheNull(c))
                .Select(c => (c.Probe.MemberId, $".NET refuses {c.Probe.Parameter}'s call for another reason: {c.DotNet}")))
            .ToLookup(entry => entry.Item1, entry => entry.Item2, StringComparer.Ordinal);

        var claims = NullArgumentSurface.AuditLines().Where(line => line.Translated).ToList();
        var unreached = new List<string>();
        var counted = 0;
        foreach (var claim in claims)
        {
            var members = NullArgumentSurface.Claimed(claim);
            if (members is null)
            {
                unreached.Add($"{claim.Id}: its labels resolve to no type");
                continue;
            }

            var taking = members.Where(m => m.GetParameters().Any(NullArgumentSurface.TakesNull)).ToList();
            if (taking.Count == 0) continue;
            counted++;
            if (taking.Any(compared.Contains)) continue;
            var reasons = taking.SelectMany(m => why[NullArgumentSurface.MemberId(claim.OwnerType, m)]).Distinct().ToList();
            unreached.Add($"{claim.Id}: {(reasons.Count == 0 ? "no probe" : string.Join(" | ", reasons))}");
        }

        output.WriteLine($"{counted} of the audit's {claims.Count} translated lines take a reference parameter.");
        Assert.True(counted > 0, "No line of the audit's record takes a reference parameter: the record was not read.");
        Assert.True(unreached.Count == 0,
            "The audit says eqc translates these, and no null reached them on both sides:\n  " + string.Join("\n  ", unreached));
    }

    // ---- The theory -----------------------------------------------------------------------------

    [SkippableFact]
    public void ANullArgument_IsAnsweredAsDotNetAnswersIt()
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        var surface = TheSurface.Value;
        var measurement = TheMeasurement.Value;

        var unmeasured = measurement.Compared.Where(c => !MeasuresTheNull(c)).ToList();
        var gaps = measurement.Compared
            .Where(MeasuresTheNull)
            .Select(c => (Compared: c, Aspect: AspectOf(c)))
            .Where(c => c.Aspect is not null)
            .ToDictionary(c => c.Compared.Probe.Id, StringComparer.Ordinal);

        output.WriteLine($"{surface.Owners.Count} types, {surface.Probes.Select(p => p.MemberId).Distinct().Count()} members, "
            + $"{surface.Probes.Count} probes: {measurement.Compared.Count - unmeasured.Count} compared on their null, "
            + $"{unmeasured.Count} whose call .NET refuses for another reason, {measurement.Refused.Count} refused by eqc, "
            + $"{measurement.Invalid.Count} that do not bind as written, and {surface.Unspoken.Count} that no value can be written for.");
        output.WriteLine($"{gaps.Count} gaps: " + string.Join(", ", gaps.Values.GroupBy(g => g.Aspect).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count()}"))
            + $"; {measurement.Compared.Count(c => c.ControlDotNet is not null)} probes ran a control.");
        foreach (var other in unmeasured) output.WriteLine($"not the null  {other.Probe.Id}: .NET {other.DotNet}");
        foreach (var refused in measurement.Refused) output.WriteLine($"refused  {refused.Probe.Id}: {refused.Why}");
        foreach (var invalid in measurement.Invalid) output.WriteLine($"invalid  {invalid.Probe.Id}: {invalid.Why}");
        foreach (var unspoken in surface.Unspoken) output.WriteLine($"unspoken {unspoken.MemberId}: {unspoken.Why}");

        var committed = Committed();
        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            Regenerate(gaps.ToDictionary(g => g.Key, g => g.Value.Aspect!, StringComparer.Ordinal), committed);
            return;
        }

        string Line(string id)
        {
            var (compared, aspect) = gaps[id];
            var line = $"{id}  [{aspect}]\n      .NET:       {compared.DotNet}\n      JavaScript: {compared.JavaScript}"
                + $"\n      C#: {compared.Probe.Statements}\n      eqc: {string.Join(" ", compared.Js.Split('\n', StringSplitOptions.TrimEntries))}";
            return aspect == "control"
                ? line + $"\n      without the null, .NET:       {compared.ControlDotNet}\n      without the null, JavaScript: {compared.ControlJavaScript}"
                : line;
        }

        var joined = gaps.Keys.Where(id => !committed.ContainsKey(id)).OrderBy(id => id, StringComparer.Ordinal).ToList();
        Assert.True(joined.Count == 0,
            $"{joined.Count} null argument(s) answered otherwise than .NET answers them, and not in the baseline. Make "
            + "the twin answer as .NET does (a refusal through the typed exception(...) with .NET's parameter name "
            + "and message), or, where that needs a decision, add the line BY HAND with its reason:\n  "
            + string.Join("\n  ", joined.Select(Line)));

        var moved = gaps.Keys.Where(id => committed.TryGetValue(id, out var entry) && entry.Aspect != gaps[id].Aspect)
            .OrderBy(id => id, StringComparer.Ordinal).ToList();
        Assert.True(moved.Count == 0,
            "A listed probe now differs in another aspect. Regenerate if it came closer to .NET, fix it if it went "
            + $"further:\n  " + string.Join("\n  ", moved.Select(id => $"{committed[id].Aspect} -> {Line(id)}")));

        var left = committed.Keys.Where(id => !gaps.ContainsKey(id)).OrderBy(id => id, StringComparer.Ordinal).ToList();
        Assert.True(left.Count == 0,
            $"{left.Count} entry(ies) of the baseline now answer as .NET does, or are no longer probed. Regenerate "
            + $"({UpdateVariable}=1) so the number moves on record:\n  " + string.Join("\n  ", left));

        var unexplained = committed.Where(entry => entry.Value.Reason.Length == 0).Select(entry => entry.Key).ToList();
        Assert.True(unexplained.Count == 0,
            $"A baseline entry without a reason. Say why .NET's answer is out of reach there, or \"{Unjudged}\":\n  "
            + string.Join("\n  ", unexplained));
    }

    /// <summary>
    /// Whether what .NET did is about the null: it returned, it refused a null (an
    /// <c>ArgumentNullException</c>, whatever it names: <c>TimeSpan.Parse(s)</c> refuses <c>'input'</c>),
    /// it refused the argument by the parameter's name, or the same call without the null returned, so
    /// the null is what made the difference. A probe whose other arguments .NET refuses on their own
    /// (<c>"abc"</c> is not a date, nor a format of one) measures those arguments, and is counted rather
    /// than compared: a canonical value cannot be a valid input of every member.
    /// </summary>
    internal static bool MeasuresTheNull(NullArgumentRun.Compared compared) =>
        !compared.DotNet.Threw
        || compared.DotNet.Type == typeof(ArgumentNullException).FullName
        || compared.DotNet.Parameter == compared.Probe.Parameter
        || compared.ControlDotNet is { Threw: false };

    /// <summary>
    /// How a probe's answer differs from .NET's, or null where it does not. A probe whose control, the
    /// same call without the null, is a call .NET answers and the browser answers otherwise is
    /// <c>control</c>: the member differs with any argument, and the null adds nothing to that. A control
    /// .NET itself refuses (the canonical arguments are not a valid call of that member) says nothing,
    /// and the probe is read on its own.
    /// </summary>
    internal static string? AspectOf(NullArgumentRun.Compared compared)
    {
        if (Aspect(compared.Probe, compared.DotNet, compared.JavaScript) is not { } aspect) return null;
        return compared is { ControlDotNet: { Threw: false } dotNet, ControlJavaScript: { } javaScript }
            && Aspect(compared.Probe, dotNet, javaScript) is not null
                ? "control"
                : aspect;
    }

    /// <summary>
    /// The first aspect in which the browser's answer differs from .NET's, or null where they agree.
    /// </summary>
    internal static string? Aspect(Probe probe, Outcome dotNet, Outcome javaScript)
    {
        if (dotNet.Threw != javaScript.Threw || dotNet.Type != javaScript.Type) return "type";
        if (dotNet.Threw)
        {
            if (dotNet.Parameter != javaScript.Parameter) return "param";
            return javaScript.Composed && dotNet.Message != javaScript.Message ? "message" : null;
        }

        return probe.ComparesValue && Canonical(dotNet.Value) != Canonical(javaScript.Value) ? "value" : null;
    }

    private static readonly JsonSerializerOptions CanonicalJson = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>One JSON text written one way: JSON.stringify and System.Text.Json escape a few
    /// characters differently, which says nothing about the value.</summary>
    private static string? Canonical(string? json) =>
        json is null ? null : JsonSerializer.Serialize(JsonDocument.Parse(json).RootElement, CanonicalJson);

    // ---- What one canonical value cannot reach -------------------------------------------------

    /// <summary>
    /// The probes hand every receiver one canonical value, a positive number and <c>true</c>, and every
    /// LINQ receiver a list, which the lowering reads as the array it is. So <c>CompareTo(object)</c>
    /// answered 1 for a null there by luck, a subtraction of null from a positive number, while
    /// <c>false</c>, a negative number and a char answered otherwise; and a sequence that is not a list
    /// is read through <c>seq</c>, which named every null <c>source</c>. Each through a typed catch,
    /// where the difference shows in a page (#569). A value still compares as it did.
    /// </summary>
    [SkippableTheory]
    [InlineData("object o = null; int n = -5; return n.CompareTo(o);")]                       // 1
    [InlineData("object o = null; long n = -5L; return n.CompareTo(o);")]                     // 1
    [InlineData("object o = null; double n = -1.5; return n.CompareTo(o);")]                  // 1
    [InlineData("object o = null; bool b = false; return b.CompareTo(o);")]                   // 1
    [InlineData("object o = null; char c = 'a'; return c.CompareTo(o);")]                     // 1
    [InlineData("object o = 3; int n = -5; return n.CompareTo(o) < 0;")]                      // true: a value compares as it did
    [InlineData("object o = null; return new DateTime(2026, 1, 2).CompareTo(o);")]            // 1
    [InlineData("object o = null; return TimeSpan.FromMinutes(-90).CompareTo(o);")]           // 1
    [InlineData("IEnumerable<int> a = null; try { return a.Zip(new[] { 1 }).Count().ToString(); } catch (ArgumentNullException e) { return e.ParamName; }")] // first
    [InlineData("HashSet<int> a = null; try { return a.Concat(new[] { 1 }).Count().ToString(); } catch (ArgumentNullException e) { return e.ParamName; }")] // first
    [InlineData("var a = new List<int> { 1 }; HashSet<int> b = null; try { return a.Concat(b).Count().ToString(); } catch (ArgumentNullException e) { return e.ParamName; }")] // second
    [InlineData("var a = new List<int> { 1 }; Func<int, int> f = null; try { return a.Max(f).ToString(); } catch (ArgumentNullException e) { return e.ParamName; }")] // selector
    [InlineData("List<int> a = null; try { return a.ToDictionary(x => x).Count.ToString(); } catch (ArgumentNullException e) { return e.ParamName; }")] // source
    [InlineData("string text = null; try { return new Guid(text).ToString(); } catch (ArgumentNullException e) { return e.ParamName; }")] // g
    public void ANullNoCanonicalProbeReaches_IsAnsweredAsDotNetAnswersIt(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    // ---- The instrument, checked against itself ------------------------------------------------

    /// <summary>
    /// The comparison names each aspect it exists to see, and passes what agrees. Outcomes written by
    /// hand, so a comparison that grew blind to one aspect fails here rather than passing a gap.
    /// </summary>
    [Fact]
    public void TheComparison_NamesEveryAspect()
    {
        var probe = new Probe("M(String) s", "M(String)", "s", "", null, ComparesValue: true, typeof(string).GetMethod("Intern")!);
        Outcome Threw(string type, string? parameter, string message, bool composed = true) => new(true, type, parameter, message, null, composed);
        Outcome Returned(string json) => new(false, null, null, null, json, true);
        var refused = Threw("System.ArgumentNullException", "s", "Value cannot be null. (Parameter 's')");

        Assert.Null(Aspect(probe, refused, refused));
        Assert.Equal("type", Aspect(probe, refused, Threw("System.NullReferenceException", null, "null is not an object", composed: false)));
        Assert.Equal("type", Aspect(probe, refused, Returned("false")));
        Assert.Equal("param", Aspect(probe, refused, Threw("System.ArgumentNullException", "source", "Value cannot be null. (Parameter 'source')")));
        Assert.Equal("message", Aspect(probe, refused, Threw("System.ArgumentNullException", "s", "Value cannot be null.")));
        Assert.Equal("value", Aspect(probe, Returned("\"\""), Returned("\"null\"")));
        // One value spelled two ways: System.Text.Json escapes a no-break space, JSON.stringify does not (#376).
        Assert.Null(Aspect(probe, Returned("\"\\u00A0\""), Returned("\"" + (char)0xA0 + "\"")));
        // An engine's own TypeError words its message as the engine does: the type is compared, not that.
        var reference = Threw("System.NullReferenceException", null, "Object reference not set to an instance of an object.");
        Assert.Null(Aspect(probe, reference, Threw("System.NullReferenceException", null, "null is not an object", composed: false)));
    }

    /// <summary>
    /// What the browser's side reads off a thrown value is what a typed catch reads: the most derived
    /// of the types the runtime gave it, its <c>ParamName</c> and its message, and an engine's TypeError
    /// as a NullReferenceException whose message is not the SDK's.
    /// </summary>
    [SkippableFact]
    public void TheBrowsersSide_ReadsAThrowAsATypedCatchDoes()
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        var program = NullArgumentRun.Describe
            + "console.log(JSON.stringify([\n"
            + "__run(() => { throw $eq.exceptions.of('System.ArgumentNullException', \"Value cannot be null. (Parameter 'x')\"); }, true),\n"
            + "__run(() => { const o = null; return o.length; }, true),\n"
            + "__run(() => 'kept', true),\n"
            + "__run(() => 'dropped', false),\n"
            + "]));\n";
        var answers = JsonDocument.Parse(JsExecutor.Run(ConformanceRunner.ImportOfWhatItNames(program) + program)).RootElement;

        Assert.Equal("""["threw","System.ArgumentNullException","x","Value cannot be null. (Parameter 'x')",true]""", answers[0].GetRawText());
        Assert.Equal("System.NullReferenceException", answers[1][1].GetString());
        Assert.False(answers[1][4].GetBoolean());
        Assert.Equal("""["returned","kept"]""", answers[2].GetRawText());
        Assert.Equal("""["returned"]""", answers[3].GetRawText());
    }

    // ---- The baseline ---------------------------------------------------------------------------

    private static string BaselinePath() => Path.Combine(RepoRoot.Find()
            ?? throw new InvalidOperationException("repository root not found"),
        "tests", "eQuantic.UI.Conformance.Tests", "null-argument-gaps.baseline.txt");

    private static Dictionary<string, (string Aspect, string Reason)> Committed()
    {
        var committed = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        if (!File.Exists(BaselinePath())) return committed;
        foreach (var line in File.ReadAllLines(BaselinePath()))
        {
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var fields = line.Split("  ", 3, StringSplitOptions.TrimEntries);
            committed[fields[0]] = (fields.Length > 1 ? fields[1] : "", fields.Length > 2 ? fields[2] : "");
        }

        return committed;
    }

    /// <summary>
    /// Rewrites the list from the measurement, carrying every reason already written across. A probe
    /// newly in the list gets <see cref="Unjudged"/> and nothing more: the regenerator cannot write the
    /// sentence that would justify it.
    /// </summary>
    private static void Regenerate(Dictionary<string, string> gaps, Dictionary<string, (string Aspect, string Reason)> committed)
    {
        var header = File.Exists(BaselinePath())
            ? File.ReadAllLines(BaselinePath()).TakeWhile(line => line.StartsWith('#')).ToList()
            : [];
        var entries = gaps.OrderBy(gap => gap.Key, StringComparer.Ordinal)
            .Select(gap => $"{gap.Key}  {gap.Value}  "
                + (committed.TryGetValue(gap.Key, out var entry) && entry.Reason.Length > 0 ? entry.Reason : Unjudged));
        File.WriteAllText(BaselinePath(), string.Join("\n", header.Concat(entries)) + "\n");
    }
}
