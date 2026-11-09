using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// What <see cref="FirstReleases"/> answers, on a repository written here with five releases (#999).
/// The wiki's marks prove the guard agrees with itself on the cases the wiki happens to hold; these
/// name the ones it must get right by construction: the order of the releases, a path, a file that
/// moved, a header-shaped line inside a hunk, a name with a space, and a checkout shaped like CI's.
/// </summary>
public class FirstReleasesTests
{
    private static readonly string[] Searches =
    [
        "class Alpha",
        "class Beta",
        "class Beta @ src/B",
        "class Gamma",
        "class Delta",
        "class Missing",
        "class Theta @ src/A",
        "class Iota @ src/A/With Space.cs",
        "class Kappa @ src/A",
        "class Lambda",
        "class Mu",
    ];

    [Fact]
    public void EachSearch_ShipsInTheFirstReleaseWhoseFilesHoldIt()
    {
        var root = Releases();
        try
        {
            var releases = FirstReleases.Read(root, Searches);

            // The first release is read against the empty tree.
            releases.Of("class Alpha").Should().Be("0.2.0-preview.1");
            // A path narrows the count: Beta was in the notes from the first release, and under
            // src/B only from preview.9.
            releases.Of("class Beta").Should().Be("0.2.0-preview.1");
            releases.Of("class Beta @ src/B").Should().Be("0.2.0-preview.9");
            // preview.9 before preview.10: read in a lexical order, preview.10 comes first and holds
            // Gamma too.
            releases.Of("class Gamma").Should().Be("0.2.0-preview.9");
            // Added and taken away between two releases: no release ever held it.
            releases.Of("class Delta").Should().BeNull();
            releases.Of("class Missing").Should().BeNull();
            // Added in a hunk right after an added line that reads like a file's header.
            releases.Of("class Theta @ src/A").Should().Be("0.2.0-preview.10");
            // A name with a space, which git ends with a tab in the header.
            releases.Of("class Iota @ src/A/With Space.cs").Should().Be("0.2.0-preview.10");
            // Moved under src/A in preview.9 without a change: a rename would carry no line.
            releases.Of("class Kappa @ src/A").Should().Be("0.2.0-preview.9");
            // A later version's preview comes after every preview of an earlier one, whatever its
            // number, and a stable release after its own previews.
            releases.Of("class Lambda").Should().Be("1.0.0-preview.2");
            releases.Of("class Mu").Should().Be("1.0.0");
        }
        finally
        {
            Remove(root);
        }
    }

    /// <summary>
    /// CI checks out one commit and fetches each tag at depth one, so every release there is a root
    /// with no history behind it. The answer reads only the releases' trees, so it is the same.
    /// </summary>
    [Fact]
    public void ACheckoutOfEachReleaseAtDepthOne_AnswersAsTheFullHistoryDoes()
    {
        var root = Releases();
        var shallow = Directory.CreateTempSubdirectory("eq-releases-shallow-").FullName;
        try
        {
            Git(shallow, "init", "--quiet");
            Git(shallow, "fetch", "--quiet", "--depth=1", new Uri(root).AbsoluteUri, "+refs/tags/v*:refs/tags/v*");
            Git(shallow, "rev-parse", "--is-shallow-repository").Trim().Should().Be("true");

            var full = FirstReleases.Read(root, Searches);
            var fetched = FirstReleases.Read(shallow, Searches);

            Searches.Select(fetched.Of).Should().Equal(Searches.Select(full.Of));
        }
        finally
        {
            Remove(root);
            Remove(shallow);
        }
    }

    /// <summary>Five releases, tagged 0.2.0-preview.1, .9 and .10, 1.0.0-preview.2 and 1.0.0, and a
    /// commit between .9 and .10 that adds what .10 takes away.</summary>
    private static string Releases()
    {
        var root = Directory.CreateTempSubdirectory("eq-releases-").FullName;
        Git(root, "init", "--quiet");

        Write(root, "src/A/One.cs", "class Alpha {}\n");
        Write(root, "docs/Notes.md", "class Beta, before it had a home\n");
        Write(root, "src/X/K.cs", "class Kappa {}\n");
        Release(root, "v0.2.0-preview.1");

        Write(root, "src/B/Two.cs", "class Beta {}\n");
        Write(root, "src/A/One.cs", "class Alpha {}\nclass Gamma {}\n");
        Directory.CreateDirectory(Path.Combine(root, "src", "A"));
        File.Move(Path.Combine(root, "src", "X", "K.cs"), Path.Combine(root, "src", "A", "K.cs"));
        Release(root, "v0.2.0-preview.9");

        Write(root, "src/A/One.cs", "class Alpha {}\nclass Gamma {}\nclass Delta {}\n");
        Commit(root);
        Write(root, "src/A/One.cs", "class Alpha {}\nclass Gamma {}\n++ reads like a header\nclass Theta {}\n");
        Write(root, "src/A/With Space.cs", "class Iota {}\n");
        Release(root, "v0.2.0-preview.10");

        Write(root, "src/C/Three.cs", "class Lambda {}\n");
        Release(root, "v1.0.0-preview.2");

        Write(root, "src/C/Four.cs", "class Mu {}\n");
        Release(root, "v1.0.0");

        return root;
    }

    private static void Write(string root, string path, string text)
    {
        var file = Path.Combine([root, .. path.Split('/')]);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, text);
    }

    private static void Commit(string root)
    {
        Git(root, "add", "--all");
        Git(root,
            "-c", "user.name=Releases", "-c", "user.email=releases@example.invalid", "-c", "commit.gpgsign=false",
            "commit", "--quiet", "--no-verify", "--message", "a release");
    }

    /// <summary>A commit and a lightweight tag: an annotated one could be signed by whatever the
    /// machine's configuration says.</summary>
    private static void Release(string root, string tag)
    {
        Commit(root);
        Git(root, "tag", tag);
    }

    private static string Git(string root, params string[] arguments)
    {
        var (exitCode, output, error) = ChildProcess.Run("git", arguments, TimeSpan.FromMinutes(1), root);
        exitCode.Should().Be(0, $"git {string.Join(' ', arguments)}: {error}");
        return output;
    }

    /// <summary>git writes its objects read-only, which Windows refuses to delete.</summary>
    private static void Remove(string root)
    {
        if (!Directory.Exists(root)) return;
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(root, recursive: true);
    }
}
