using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The served runtime's budget as it is recorded (#1489): a BASE the last release folded, in
/// <c>Budgets/served-runtime.json</c>, and a DELTA per change that moved the runtime past what the
/// test allows, in <c>Budgets/served-runtime/&lt;change&gt;.json</c>, each saying what its bytes bought.
/// The budget a build is held to is the base plus every delta.
/// <para>
/// One number every change re-recorded made any two such pull requests conflict on its one line, and
/// the second to land had to measure again on the new main. A file per change cannot conflict, and a
/// release folds the deltas into the base (<c>scripts/fold-runtime-budget.sh</c>), as fold-ledger.sh
/// folds <c>docs/ledger/</c>.
/// </para>
/// </summary>
internal sealed partial class RuntimeBudget
{
    public const string BaseFile = "tests/eQuantic.UI.Server.Tests/Budgets/served-runtime.json";
    public const string DeltaFolder = "tests/eQuantic.UI.Server.Tests/Budgets/served-runtime";
    public const string MeasuredWith = "gzip, CompressionLevel.SmallestSize";

    private RuntimeBudget(long baseBytes, string measuredWith, IReadOnlyList<Delta> deltas)
    {
        BaseBytes = baseBytes;
        BaseMeasuredWith = measuredWith;
        Deltas = deltas;
    }

    /// <summary>The bytes the last release folded.</summary>
    public long BaseBytes { get; }

    /// <summary>How the base was measured, which a size measured another way is not comparable with.</summary>
    public string BaseMeasuredWith { get; }

    /// <summary>Each change's delta, by the change's name.</summary>
    public IReadOnlyList<Delta> Deltas { get; }

    /// <summary>What a build is held to: the base and every delta.</summary>
    public long Bytes => BaseBytes + Deltas.Sum(delta => delta.Bytes);

    /// <summary>One change's delta: what it added (or saved, below zero) and what that bought.</summary>
    public sealed record Delta(string Change, long Bytes, string Bought);

    /// <summary>The budget recorded under <paramref name="root"/>.</summary>
    public static RuntimeBudget Load(string root)
    {
        using var record = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, BaseFile)));
        var deltas = new List<Delta>();
        var folder = Path.Combine(root, DeltaFolder);
        if (Directory.Exists(folder))
        {
            foreach (var file in Directory.GetFiles(folder, "*.json").Order(StringComparer.Ordinal))
            {
                using var delta = JsonDocument.Parse(File.ReadAllText(file));
                deltas.Add(new Delta(
                    Path.GetFileNameWithoutExtension(file),
                    delta.RootElement.GetProperty("deltaBytes").GetInt64(),
                    delta.RootElement.TryGetProperty("bought", out var bought) ? bought.GetString() ?? "" : ""));
            }
        }

        return new RuntimeBudget(
            record.RootElement.GetProperty("gzipBytes").GetInt64(),
            record.RootElement.GetProperty("measuredWith").GetString() ?? "",
            deltas);
    }

    /// <summary>
    /// Writes the delta of <paramref name="change"/> under <paramref name="root"/>: what
    /// <paramref name="measured"/> adds to the base and every OTHER change's delta, so a change measured
    /// again replaces its own file rather than adding to it.
    /// </summary>
    public static void Record(string root, string change, long measured, string bought)
    {
        var others = Load(root).Deltas.Where(delta => delta.Change != change);
        var budget = Load(root).BaseBytes + others.Sum(delta => delta.Bytes);
        var folder = Path.Combine(root, DeltaFolder);
        Directory.CreateDirectory(folder);
        var json = JsonSerializer.Serialize(new { deltaBytes = measured - budget, bought },
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(folder, change + ".json"), json + "\n");
    }

    /// <summary>
    /// The change a delta is recorded for: <c>EQ_BUDGET_CHANGE</c>, or the branch checked out, whole and
    /// with its <c>/</c> as <c>-</c>, so two branches never share a file. Null on main or a detached
    /// HEAD, where no change is named.
    /// </summary>
    public static string? ChangeName(string root)
    {
        var name = Environment.GetEnvironmentVariable("EQ_BUDGET_CHANGE");
        if (string.IsNullOrWhiteSpace(name))
        {
            using var git = Process.Start(new ProcessStartInfo("git", ["-C", root, "rev-parse", "--abbrev-ref", "HEAD"])
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            })!;
            name = git.StandardOutput.ReadToEnd().Trim();
            git.WaitForExit();
            if (git.ExitCode != 0 || name is "" or "HEAD" or "main") return null;
        }

        name = name.Replace('/', '-');
        return ChangeShape().IsMatch(name) ? name : null;
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]*$")]
    private static partial Regex ChangeShape();

    public static string Text(long bytes) => bytes.ToString("N0", CultureInfo.InvariantCulture);
}
