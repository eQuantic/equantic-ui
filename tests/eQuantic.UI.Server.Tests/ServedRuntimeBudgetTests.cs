using System.IO.Compression;
using Xunit.Abstractions;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The served runtime's size is a BUDGET: recorded in the tree, compared on every build, and moved
/// only by a change that says why (#290).
/// <para>
/// What is measured is what the Server embeds and answers at <c>/_equantic/runtime.js</c>, which
/// since #335 is the only runtime there is. Every app downloads these bytes, and the shared
/// component library ships inside them, so a component added to the SDK is a cost every page pays
/// before its own module loads. The number is gzip at the smallest-size level, the unit a bundle
/// budget is quoted in. The raw size is printed and not recorded: a number in the tree that nothing
/// compares drifts into a false one.
/// </para>
/// <para>
/// Growth past <see cref="Growth"/> fails, and so does a shrink past <see cref="Shrink"/>. The first
/// makes a heavier runtime a decision; the second locks a win in, rather than leaving headroom the
/// next change spends without anyone deciding to. Both are measured against the base and every
/// change's delta (<see cref="RuntimeBudget"/>, #1489). A change that crosses either writes its own
/// delta, <c>EQ_UPDATE_RUNTIME_BUDGET=1</c> with <c>EQ_RUNTIME_BOUGHT</c> saying what the bytes
/// bought, and a shrink writes a negative one. A release folds the deltas into the base.
/// </para>
/// </summary>
public class ServedRuntimeBudgetTests
{
    private const string MeasuredWith = RuntimeBudget.MeasuredWith;

    /// <summary>What a change may add before the record has to move: about 1.4 KB at the size this
    /// was introduced at, less than a component, so a component added to the runtime states its cost.</summary>
    private const double Growth = 0.01;

    /// <summary>What a change may save before the record has to follow it down.</summary>
    private const double Shrink = 0.05;

    private readonly ITestOutputHelper _output;

    public ServedRuntimeBudgetTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void TheServedRuntime_StaysInsideItsBudget()
    {
        var gzip = Gzip(Served());
        var root = Repository.Root();

        if (Environment.GetEnvironmentVariable("EQ_UPDATE_RUNTIME_BUDGET") == "1")
        {
            var change = RuntimeBudget.ChangeName(root);
            change.Should().NotBeNull("a delta is recorded for a change: run it on the change's branch, "
                + "or name the change with EQ_BUDGET_CHANGE");
            var bought = Environment.GetEnvironmentVariable("EQ_RUNTIME_BOUGHT");
            bought.Should().NotBeNullOrWhiteSpace("a delta says what its bytes bought: set EQ_RUNTIME_BOUGHT");
            RuntimeBudget.Record(root, change!, gzip, bought!);
        }

        File.Exists(Path.Combine(root, RuntimeBudget.BaseFile)).Should().BeTrue(
            $"the budget's base is recorded at {RuntimeBudget.BaseFile}");
        var budget = RuntimeBudget.Load(root);
        budget.BaseMeasuredWith.Should().Be(MeasuredWith, "a size measured another way is not comparable with this one");

        var summary = $"the served runtime is {RuntimeBudget.Text(gzip)} bytes gzipped, and its budget is "
            + $"{RuntimeBudget.Text(budget.Bytes)}: the base of {RuntimeBudget.Text(budget.BaseBytes)} and "
            + $"{budget.Deltas.Count} change(s)' deltas";
        _output.WriteLine(summary);

        gzip.Should().BeLessOrEqualTo((long)Math.Floor(budget.Bytes * (1 + Growth)),
            $"{summary}. It grew more than {Growth * 100:0}%: if that is intended, record the change's delta with "
            + "EQ_UPDATE_RUNTIME_BUDGET=1 and EQ_RUNTIME_BOUGHT saying what the bytes bought");
        gzip.Should().BeGreaterOrEqualTo((long)Math.Ceiling(budget.Bytes * (1 - Shrink)),
            $"{summary}. It shrank more than {Shrink * 100:0}%: lock the win in with a negative delta, "
            + "EQ_UPDATE_RUNTIME_BUDGET=1 and EQ_RUNTIME_BOUGHT saying what was saved");
    }

    /// <summary>A delta is a decision, so it says what it bought, under a change's name.</summary>
    [Fact]
    public void EveryDelta_SaysWhatItsBytesBought()
    {
        var budget = RuntimeBudget.Load(Repository.Root());
        budget.Deltas.Where(delta => string.IsNullOrWhiteSpace(delta.Bought)).Select(delta => delta.Change)
            .Should().BeEmpty("a change past the budget says what its bytes bought (`bought`)");
        budget.Deltas.Where(delta => delta.Bytes == 0).Select(delta => delta.Change)
            .Should().BeEmpty("a delta of no bytes records nothing: delete it");
    }

    /// <summary>
    /// The budget is the base and every delta, and a change measured again replaces its own delta
    /// rather than adding to it, so two changes in flight each write a file of their own and land in
    /// either order (#1489).
    /// </summary>
    [Fact]
    public void TheBudget_IsTheBaseAndEveryDelta_AndAChangeReplacesItsOwn()
    {
        var root = Directory.CreateTempSubdirectory("eq-budget-").FullName;
        try
        {
            var record = Path.Combine(root, RuntimeBudget.BaseFile);
            Directory.CreateDirectory(Path.GetDirectoryName(record)!);
            File.WriteAllText(record, $"{{ \"gzipBytes\": 1000, \"measuredWith\": \"{MeasuredWith}\" }}");

            RuntimeBudget.Record(root, "feat-a-grows", 1030, "a component");
            RuntimeBudget.Record(root, "fix-b-shrinks", 1020, "a smaller lowering");
            RuntimeBudget.Load(root).Bytes.Should().Be(1020, "the base and both deltas, +30 and -10");

            RuntimeBudget.Record(root, "feat-a-grows", 1040, "a component, measured again");
            var budget = RuntimeBudget.Load(root);
            budget.Deltas.Should().HaveCount(2, "a change measured again replaces its own delta");
            budget.Deltas.Single(delta => delta.Change == "feat-a-grows").Bytes.Should().Be(50,
                "measured against the base and the OTHER change's delta: 1040 - (1000 - 10)");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>The bytes the Server answers with, from the resource <c>UIExtensions</c> serves.</summary>
    private static byte[] Served()
    {
        using var stream = typeof(UIExtensions).Assembly.GetManifestResourceStream("eQuantic.UI.Server.runtime.js");
        stream.Should().NotBeNull("the Server embeds the bundle under the name UIExtensions serves it by");
        using var copy = new MemoryStream();
        stream!.CopyTo(copy);
        copy.Length.Should().BeGreaterThan(0, "an empty runtime would sit inside any budget");
        return copy.ToArray();
    }

    private static long Gzip(byte[] bytes)
    {
        using var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            gzip.Write(bytes);
        return compressed.Length;
    }
}
