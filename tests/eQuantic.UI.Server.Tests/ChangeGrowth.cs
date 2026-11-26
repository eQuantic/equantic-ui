using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// What a change itself added to the served runtime (#1922): the bundle this checkout serves, against the bundle
/// of the commit the change is measured from, built from that commit's sources with the embedded bun and the
/// arguments the Server's <c>BundleRuntime</c> builds its own with. The base and the deltas only say what the
/// runtime may weigh, so growth under their 1% margin passed unrecorded, and the first change to cross it was
/// handed everyone else's bytes: #1913, whose own growth was 106 bytes, met a ceiling 3,907 bytes of others' growth
/// had brought down to it.
/// </summary>
internal static class ChangeGrowth
{
    private const string Runtime = "src/eQuantic.UI.Runtime";
    private const string Resources = "src/eQuantic.UI.Sdk/Resources";
    private const string Server = "src/eQuantic.UI.Server/eQuantic.UI.Server.csproj";

    /// <summary>
    /// The commit a change is measured from: <c>EQ_RUNTIME_BASE</c>; on a named branch, where it leaves
    /// <c>origin/main</c>; anywhere else, the checkout's first parent, which is <c>main</c> in a pull request's
    /// merge ref, what the queue already holds in a merge queue's run, and the commit before on <c>main</c>. Null
    /// where none can be read, a checkout of one commit with no parent (CI's test job fetches two).
    /// </summary>
    public static string? Base(string root)
    {
        if (Environment.GetEnvironmentVariable("EQ_RUNTIME_BASE") is { Length: > 0 } given) return Commit(root, given);
        if (RuntimeBudget.ChangeName(root) is not null && Git(root, "merge-base", "HEAD", "origin/main") is { } forked)
            return forked;
        return Commit(root, "HEAD^1");
    }

    /// <summary>The bundle of <paramref name="commit"/>'s runtime, built as the Server's BundleRuntime builds it.</summary>
    public static byte[] BundleAt(string root, string commit)
    {
        var work = Directory.CreateTempSubdirectory("eq-runtime-base-").FullName;
        try
        {
            var archive = Path.Combine(work, "sources.zip");
            var (archived, _, archiveError) = ChildProcess.Run("git",
                ["-C", root, "archive", "--format=zip", "-o", archive, commit, Runtime, Resources, Server], TimeSpan.FromMinutes(2));
            archived.Should().Be(0, $"git archives {commit}'s runtime: {archiveError}");
            ZipFile.ExtractToDirectory(archive, work);

            var bun = EmbeddedBun.Program(root);
            bun.Should().NotBeNull("the source tree carries this machine's bun, which bundles the base as it bundles the runtime");
            var server = Path.Combine(work, Path.GetDirectoryName(Server)!);
            var (code, output, error) = ChildProcess.Run(bun!, BundleArguments(Path.Combine(work, Server)),
                TimeSpan.FromMinutes(5), workingDirectory: server);
            code.Should().Be(0, $"bun bundles {commit}'s runtime: {output}{error}");
            return File.ReadAllBytes(Path.Combine(server, "wwwroot", "runtime.js"));
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }

    /// <summary>
    /// The bytes the change records for itself: each delta of this checkout that <paramref name="basis"/> does not
    /// hold, and what each it holds moved by. A delta the base held unchanged is another change's, and one the
    /// change removed is a release's fold.
    /// </summary>
    public static long RecordedSince(string root, string basis)
    {
        var folder = Path.Combine(root, RuntimeBudget.DeltaFolder);
        if (!Directory.Exists(folder)) return 0;
        long recorded = 0;
        foreach (var file in Directory.GetFiles(folder, "*.json"))
        {
            var path = $"{RuntimeBudget.DeltaFolder}/{Path.GetFileName(file)}";
            var before = Git(root, "show", $"{basis}:{path}") is { } held ? Bytes(held) : 0;
            recorded += Bytes(File.ReadAllText(file)) - before;
        }
        return recorded;
    }

    /// <summary>
    /// What is wrong with a change that moved the runtime by <paramref name="grown"/> and records
    /// <paramref name="recorded"/> for itself, or null: growth, however small, records a delta of its own, and so
    /// does a shrink, whose bytes the budget would otherwise keep as headroom until a later shrink met its floor
    /// with a record that cannot reach it; and the one it records stays within <paramref name="noise"/> of what it
    /// moved, the 1% the budget keeps for measurement noise, as a change measured on an older <c>main</c> meets the
    /// newer one's neighbours.
    /// </summary>
    public static string? Owed(long grown, long recorded, long noise)
    {
        if (grown != 0 && recorded == 0)
            return $"this change {(grown > 0 ? "grows" : "shrinks")} the served runtime by {RuntimeBudget.Text(Math.Abs(grown))} "
                + "bytes gzipped and records no delta of its own: record it with EQ_UPDATE_RUNTIME_BUDGET=1 and "
                + "EQ_RUNTIME_BOUGHT saying what the bytes bought or saved, however small (#1922)";
        if (recorded != 0 && Math.Abs(recorded - grown) > noise)
            return $"this change records {RuntimeBudget.Text(recorded)} bytes for itself and grows the served runtime by "
                + $"{RuntimeBudget.Text(grown)}, more than the {RuntimeBudget.Text(noise)} the budget keeps for noise apart: "
                + "measure it again with EQ_UPDATE_RUNTIME_BUDGET=1";
        return null;
    }

    /// <summary>The arguments BundleRuntime hands bun, read from the Server's project, in its order.</summary>
    private static IReadOnlyList<string> BundleArguments(string project)
    {
        var arguments = new List<string>();
        foreach (var item in XDocument.Load(project).Descendants("_EqBundleArgument"))
        {
            if (item.Attribute("Option")?.Value is { } option) arguments.Add(option);
            arguments.Add(item.Attribute("Value")!.Value);
        }
        arguments.Should().NotBeEmpty("the Server's project declares the arguments BundleRuntime hands bun");
        return arguments;
    }

    private static long Bytes(string json)
    {
        using var delta = JsonDocument.Parse(json);
        return delta.RootElement.GetProperty("deltaBytes").GetInt64();
    }

    private static string? Commit(string root, string revision) => Git(root, "rev-parse", "--verify", "--quiet", revision + "^{commit}");

    private static string? Git(string root, params string[] arguments)
    {
        var (code, output, _) = ChildProcess.Run("git", ["-C", root, .. arguments], TimeSpan.FromMinutes(1));
        return code == 0 && output.Trim() is { Length: > 0 } answer ? answer : null;
    }
}
