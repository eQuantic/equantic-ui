using System.Reflection;
using eQuantic.UI.Compiler.CodeGen.Strategies;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Coverage;

/// <summary>
/// The expression IR migration as a NUMBER, with a committed baseline: the strategies that still
/// return text. A strategy may only leave this list (update the baseline when it crosses over);
/// one may never join it — a new strategy is born on the IR, implementing
/// <see cref="IExpressionIrStrategy"/>. The statement side has no text contract left at all.
/// Regenerate with EQ_UPDATE_IR_BASELINE=1.
/// </summary>
public class IrMigrationCoverageTests
{
    private static readonly Assembly Compiler = typeof(IConversionStrategy).Assembly;

    private static IReadOnlyList<string> TextExpressionStrategies() => Compiler.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IConversionStrategy).IsAssignableFrom(t))
        .Where(t => !typeof(IExpressionIrStrategy).IsAssignableFrom(t))
        .Select(t => t.Name)
        .OrderBy(n => n, StringComparer.Ordinal)
        .ToList();

    [Fact]
    public void EveryStatementStrategy_BuildsIr()
    {
        // The contract itself returns IR now; this pins that no text-returning statement path exists.
        var convert = typeof(IStatementStrategy).GetMethod(nameof(IStatementStrategy.Convert))!;
        Assert.Equal(typeof(eQuantic.UI.Compiler.CodeGen.Ir.JsStatement), convert.ReturnType);
    }

    [Fact]
    public void TextExpressionStrategies_OnlyEverLeaveTheBaseline()
    {
        var current = TextExpressionStrategies();
        var report = string.Join("\n", current) + "\n";
        var baselinePath = Path.Combine(RepoRoot(), "tests", "eQuantic.UI.Compiler.Tests", "Coverage", "ir-migration.baseline.txt");

        if (Environment.GetEnvironmentVariable("EQ_UPDATE_IR_BASELINE") == "1")
        {
            File.WriteAllText(baselinePath, report);
            return;
        }

        Assert.True(File.Exists(baselinePath),
            "No committed baseline — run once with EQ_UPDATE_IR_BASELINE=1 and commit the file.");
        var committed = File.ReadAllText(baselinePath).Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);

        var joined = current.Where(n => !committed.Contains(n)).ToList();
        var left = committed.Where(n => !current.Contains(n)).ToList();

        Assert.True(joined.Count == 0,
            "A strategy returning TEXT was added — new strategies are born on the IR (implement "
            + "IExpressionIrStrategy):\n  " + string.Join("\n  ", joined));
        Assert.True(left.Count == 0,
            $"{left.Count} strategy(ies) crossed over to the IR — regenerate the baseline "
            + "(EQ_UPDATE_IR_BASELINE=1) so the number moves on record:\n  " + string.Join("\n  ", left));
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "eQuantic.UI.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("repository root not found");
    }
}
