namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The served runtime's budget measures a change against the checkout's first parent (<see cref="ChangeGrowth"/>),
/// which is why CI checks out two commits deep. Every job that runs it then fetches the release tags for the
/// version-mark guard, and on a shallow clone <c>git fetch --depth</c> sets each fetched tip's history to that
/// depth, so a tag at HEAD sets HEAD's. A release commit is a tag's own tip, and the tags fetched one deep cut it
/// to a root: the tag's run and main's run of <c>v0.2.0-preview.73</c> lost the parent and published nothing,
/// while every commit no tag points at passed (#1930).
/// <para>
/// So the checkout is rebuilt here as CI builds it, a tag at HEAD, two commits fetched, and every tag fetch the
/// workflow runs is read from <c>ci.yml</c> and run on it as written.
/// </para>
/// </summary>
public class ReleaseCheckoutTests
{
    [Fact]
    public void EveryTagFetchOfTheWorkflow_KeepsTheParentOfACommitItsTagPointsAt()
    {
        var fetches = TagFetches(File.ReadAllLines(Path.Combine(Repository.Root(), ".github", "workflows", "ci.yml")));
        fetches.Should().NotBeEmpty("the test jobs fetch the release tags for the version-mark guard, and a guard "
            + "that finds none checks nothing");

        var origin = Directory.CreateTempSubdirectory("eq-release-origin-").FullName;
        var checkout = Directory.CreateTempSubdirectory("eq-release-checkout-").FullName;
        try
        {
            Git(origin, "init", "--quiet");
            var parent = Commit(origin, "before the release");
            var release = Commit(origin, "the release");
            Git(origin, "tag", "-a", "v1.0.0", "-m", "1.0.0", release);

            // What actions/checkout does with fetch-depth: 2.
            Git(checkout, "init", "--quiet");
            Git(checkout, "remote", "add", "origin", new Uri(origin).AbsoluteUri);
            Git(checkout, "fetch", "--quiet", "--no-tags", "--depth=2", "origin", $"+{release}:refs/remotes/origin/main");
            Git(checkout, "checkout", "--quiet", "--detach", release);
            Parent(checkout).Should().Be(parent, "the checkout holds the commit before the release");

            foreach (var fetch in fetches)
            {
                Git(checkout, fetch);
                Parent(checkout).Should().Be(parent,
                    $"`git {string.Join(' ', fetch)}` must leave the release commit's parent in the checkout, "
                    + "the commit the served runtime's budget measures it from: a tag at HEAD fetched one deep "
                    + "cuts HEAD to a root");
            }
        }
        finally
        {
            Remove(origin);
            Remove(checkout);
        }
    }

    /// <summary>The arguments of every <c>git fetch</c> of the release tags a step of the workflow runs.</summary>
    private static List<string[]> TagFetches(IEnumerable<string> workflow) =>
        workflow
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("run: git fetch ", StringComparison.Ordinal) && line.Contains("refs/tags/v*"))
            .Select(line => line["run: git ".Length..]
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => word.Trim('\''))
                .ToArray())
            .ToList();

    private static string Commit(string repository, string message)
    {
        File.AppendAllText(Path.Combine(repository, "file.txt"), message + "\n");
        Git(repository, "add", "file.txt");
        Git(repository, "commit", "--quiet", "-m", message);
        return Git(repository, "rev-parse", "HEAD").Trim();
    }

    private static string? Parent(string checkout)
    {
        var (code, output, _) = ChildProcess.Run("git", ["-C", checkout, "rev-parse", "--verify", "--quiet", "HEAD^1^{commit}"],
            TimeSpan.FromMinutes(1));
        return code == 0 ? output.Trim() : null;
    }

    private static string Git(string repository, params string[] arguments)
    {
        string[] all = ["-C", repository, "-c", "user.name=Test", "-c", "user.email=test@example.com", "-c", "commit.gpgsign=false",
            "-c", "tag.gpgsign=false", .. arguments];
        var (code, output, error) = ChildProcess.Run("git", all, TimeSpan.FromMinutes(1));
        code.Should().Be(0, $"git {string.Join(' ', arguments)}: {error}");
        return output;
    }

    private static void Remove(string directory)
    {
        if (!Directory.Exists(directory)) return;
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(directory, recursive: true);
    }
}
