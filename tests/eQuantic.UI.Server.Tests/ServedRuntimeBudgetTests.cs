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
/// A change that grows the runtime records its own delta, however small: the bytes it grew it by against the
/// commit it is measured from (<see cref="ChangeGrowth"/>, #1922), <c>EQ_UPDATE_RUNTIME_BUDGET=1</c> with
/// <c>EQ_RUNTIME_BOUGHT</c> saying what the bytes bought. Growth under the 1% margin passed unrecorded, so the
/// first change past it paid for everyone's. The margin stays as what it is for, measurement noise: the runtime
/// past <see cref="Growth"/> over the base and every delta fails (<see cref="RuntimeBudget"/>, #1489), and so does
/// a shrink past <see cref="Shrink"/>, which locks a win in with a negative delta rather than leaving headroom the
/// next change spends without anyone deciding to. A release folds the deltas into the base.
/// </para>
/// </summary>
public class ServedRuntimeBudgetTests
{
    private const string MeasuredWith = RuntimeBudget.MeasuredWith;

    /// <summary>What the runtime may weigh over the base and every delta before it fails: the noise of deltas
    /// each measured beside a <c>main</c> of its own, never an allowance, since a change records its growth
    /// however small (#1922).</summary>
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

        if (Recording) RecordOwn(root, Grown(root, gzip));

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

    /// <summary>
    /// A change that grows the runtime records a delta of its own, however small, and the one it records is what
    /// it grew it by, within the noise the budget keeps (#1922). Measured against the commit the change is measured
    /// from, so a change that never touched the runtime owes nothing for what <c>main</c> did.
    /// </summary>
    [Fact]
    public void AChangeThatGrowsTheRuntime_RecordsItsOwnDelta()
    {
        var root = Repository.Root();
        var (basis, grown) = Grown(root, Gzip(Served()));
        // The guard is the test that asks for the record, so it writes it too, run alone or first.
        if (Recording) RecordOwn(root, (basis, grown));
        var recorded = ChangeGrowth.RecordedSince(root, basis);
        var budget = RuntimeBudget.Load(root);
        _output.WriteLine($"grown by {RuntimeBudget.Text(grown)} bytes from {basis}, recorded {RuntimeBudget.Text(recorded)}");

        ChangeGrowth.Owed(grown, recorded, Noise(budget)).Should().BeNull();
    }

    /// <summary>The rule the guard applies, apart from a build: growth owes a delta, however small, and so does a
    /// shrink; a delta is what its change moved, within the noise the budget keeps; a change that moves nothing owes
    /// nothing.</summary>
    [Theory]
    [InlineData(3, 0, "grows the served runtime by 3 bytes gzipped and records no delta")]
    [InlineData(1_000, 0, "grows the served runtime by 1,000 bytes gzipped and records no delta")]
    [InlineData(120, 5_000, "records 5,000 bytes for itself and grows the served runtime by 120")]
    [InlineData(0, 400, "records 400 bytes for itself and grows the served runtime by 0")]
    [InlineData(3, 3, null)]
    [InlineData(106, 128, null)]
    [InlineData(0, 0, null)]
    [InlineData(-200, 0, "shrinks the served runtime by 200 bytes gzipped and records no delta")]
    [InlineData(-200, -200, null)]
    public void GrowthOwesADeltaOfItsOwn_HoweverSmall(long grown, long recorded, string? owed)
    {
        var verdict = ChangeGrowth.Owed(grown, recorded, noise: 100);
        if (owed is null) verdict.Should().BeNull();
        else verdict.Should().Contain(owed);
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

            // No delta folder yet, as after a release's fold: a change that grows nothing records nothing, and throws nothing.
            RuntimeBudget.Record(root, "feat-none", 0, "nothing");
            Directory.Exists(Path.Combine(root, RuntimeBudget.DeltaFolder)).Should().BeFalse();

            RuntimeBudget.Record(root, "feat-a-grows", 30, "a component");
            RuntimeBudget.Record(root, "fix-b-shrinks", -10, "a smaller lowering");
            RuntimeBudget.Load(root).Bytes.Should().Be(1020, "the base and both deltas, +30 and -10");

            RuntimeBudget.Record(root, "feat-a-grows", 50, "a component, measured again");
            var budget = RuntimeBudget.Load(root);
            budget.Deltas.Should().HaveCount(2, "a change measured again replaces its own delta");
            budget.Deltas.Single(delta => delta.Change == "feat-a-grows").Bytes.Should().Be(50,
                "what the change grew the runtime by, never what main grew around it (#1922)");

            RuntimeBudget.Record(root, "feat-a-grows", 0, "a component, taken out again");
            RuntimeBudget.Load(root).Deltas.Select(delta => delta.Change).Should().Equal(["fix-b-shrinks"],
                "a change that grows nothing loses its delta rather than recording no bytes");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>The commit this checkout's change is measured from and the bytes it grew the runtime by since.</summary>
    private static (string Basis, long Grown) Grown(string root, long served)
    {
        var basis = ChangeGrowth.Base(root);
        basis.Should().NotBeNull("a change is measured against the commit it is measured from: EQ_RUNTIME_BASE, the "
            + "branch's fork from origin/main, or the checkout's first parent, which a checkout of one commit does not "
            + "hold (CI's test job fetches two)");
        return (basis!, served - Gzip(ChangeGrowth.BundleAt(root, basis!)));
    }

    /// <summary>Whether the run records the change's delta: <c>EQ_UPDATE_RUNTIME_BUDGET=1</c>.</summary>
    private static bool Recording => Environment.GetEnvironmentVariable("EQ_UPDATE_RUNTIME_BUDGET") == "1";

    /// <summary>Writes the change's own growth as its delta, saying what <c>EQ_RUNTIME_BOUGHT</c> says it bought.</summary>
    private void RecordOwn(string root, (string Basis, long Grown) measured)
    {
        var change = RuntimeBudget.ChangeName(root);
        change.Should().NotBeNull("a delta is recorded for a change: run it on the change's branch, "
            + "or name the change with EQ_BUDGET_CHANGE");
        var bought = Environment.GetEnvironmentVariable("EQ_RUNTIME_BOUGHT");
        bought.Should().NotBeNullOrWhiteSpace("a delta says what its bytes bought: set EQ_RUNTIME_BOUGHT");
        _output.WriteLine($"recording {RuntimeBudget.Text(measured.Grown)} bytes for {change}, measured from {measured.Basis}");
        RuntimeBudget.Record(root, change!, measured.Grown, bought!);
    }

    /// <summary>The margin the budget keeps for measurement noise, in bytes.</summary>
    private static long Noise(RuntimeBudget budget) => (long)Math.Floor(budget.Bytes * Growth);

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
