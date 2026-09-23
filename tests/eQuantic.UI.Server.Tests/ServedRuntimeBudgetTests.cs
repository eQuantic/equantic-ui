using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using Xunit.Abstractions;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The served runtime's size is a BUDGET: recorded in the tree, compared on every build, and moved
/// only by a commit that says why (#290).
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
/// next change spends without anyone deciding to. <c>EQ_UPDATE_RUNTIME_BUDGET=1</c> writes the
/// record, and the commit that does says what moved it.
/// </para>
/// </summary>
public class ServedRuntimeBudgetTests
{
    private const string Record = "tests/eQuantic.UI.Server.Tests/Budgets/served-runtime.json";
    private const string MeasuredWith = "gzip, CompressionLevel.SmallestSize";

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
        var served = Served();
        var gzip = Gzip(served);
        var path = Path.Combine(Repository.Root(), Record);

        if (Environment.GetEnvironmentVariable("EQ_UPDATE_RUNTIME_BUDGET") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path,
                $"{{\n  \"gzipBytes\": {gzip.ToString(CultureInfo.InvariantCulture)},\n  \"measuredWith\": \"{MeasuredWith}\"\n}}\n");
        }

        File.Exists(path).Should().BeTrue($"the budget is recorded at {Record}; write it with EQ_UPDATE_RUNTIME_BUDGET=1");
        using var record = JsonDocument.Parse(File.ReadAllText(path));
        record.RootElement.GetProperty("measuredWith").GetString().Should().Be(MeasuredWith,
            "a size measured another way is not comparable with this one");
        var budget = record.RootElement.GetProperty("gzipBytes").GetInt64();

        var summary = $"the served runtime is {Bytes(served.Length)} bytes, {Bytes(gzip)} gzipped, "
            + $"and its budget records {Bytes(budget)}";
        _output.WriteLine(summary);

        gzip.Should().BeLessOrEqualTo((long)Math.Floor(budget * (1 + Growth)),
            $"{summary}. It grew more than {Growth * 100:0}%: if that is intended, regenerate with "
            + "EQ_UPDATE_RUNTIME_BUDGET=1 and say in the commit what the bytes bought");
        gzip.Should().BeGreaterOrEqualTo((long)Math.Ceiling(budget * (1 - Shrink)),
            $"{summary}. It shrank more than {Shrink * 100:0}%: lock the win in with EQ_UPDATE_RUNTIME_BUDGET=1");
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

    private static string Bytes(long count) => count.ToString("N0", CultureInfo.InvariantCulture);
}
