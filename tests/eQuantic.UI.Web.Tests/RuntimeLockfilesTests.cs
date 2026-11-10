using System.Text.Json;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The runtime's TypeScript keeps two lockfiles for one manifest, and only one of them is installed.
/// <c>TestRuntime</c>, and CI's <c>test-runtime</c> job through it, install from
/// <c>src/eQuantic.UI.Runtime/bun.lock</c> with <c>--frozen-lockfile</c>. GitHub's dependency graph
/// reads no <c>bun.lock</c>, so Dependabot watches <c>package-lock.json</c> beside it, which nothing
/// installs from.
/// <para>
/// <c>bun.lock</c> is the authority and <c>package-lock.json</c> is its mirror, kept so that the
/// advisories reported are the ones of the tree that runs. A mirror kept by hand drifts silently:
/// #107 found it three majors behind its manifest, and an advisory was reported closed while the tree
/// CI ran still carried it (#1180). So the two are compared here, package for package and version for
/// version, and a fix that updates one of them alone fails, naming what differs.
/// </para>
/// <para>
/// The comparison is by resolved <c>name@version</c>, not by path: bun and npm hoist the same tree
/// differently, and where a package sits in <c>node_modules</c> decides nothing about what is
/// installed. The manifest is compared too, because a range changed in <c>package.json</c> and
/// resolved by one tool only is the drift's first step.
/// </para>
/// </summary>
public class RuntimeLockfilesTests
{
    /// <summary>
    /// A floor against the vacuous pass: two lockfiles that both lost their packages would compare
    /// equal. The runtime's test tooling resolves over two hundred, and fifty only has to catch "empty".
    /// </summary>
    private const int MinimumPackages = 50;

    private static readonly JsonDocumentOptions BunLockOptions = new()
    {
        // bun.lock is JSON with trailing commas, which System.Text.Json refuses unless told.
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    [Fact]
    public void BunLock_AndItsMirror_ResolveTheSamePackages()
    {
        var runtime = RuntimeDirectory();
        var bun = BunLockPackages(Path.Combine(runtime, "bun.lock"));
        var npm = PackageLockPackages(Path.Combine(runtime, "package-lock.json"));

        bun.Count.Should().BeGreaterThanOrEqualTo(MinimumPackages, "bun.lock is the tree the runtime's tests install");
        npm.Count.Should().BeGreaterThanOrEqualTo(MinimumPackages, "package-lock.json mirrors that tree for Dependabot");

        var onlyInBun = bun.Except(npm).Order(StringComparer.Ordinal).ToList();
        var onlyInNpm = npm.Except(bun).Order(StringComparer.Ordinal).ToList();

        (onlyInBun.Count + onlyInNpm.Count).Should().Be(0,
            "src/eQuantic.UI.Runtime/package-lock.json mirrors bun.lock, the lockfile the runtime installs "
            + "from, so Dependabot reports the advisories of the tree that runs. Update both in the same "
            + "change (`bun install` and `npm install --package-lock-only`, from src/eQuantic.UI.Runtime). "
            + "Only in bun.lock: [{0}]. Only in package-lock.json: [{1}]",
            string.Join(", ", onlyInBun),
            string.Join(", ", onlyInNpm));
    }

    /// <summary>The sections of a manifest that a lockfile resolves.</summary>
    private static readonly string[] Sections =
        ["dependencies", "devDependencies", "optionalDependencies", "peerDependencies"];

    [Fact]
    public void BothLockfiles_RecordTheManifestsRanges()
    {
        var runtime = RuntimeDirectory();
        var manifest = Root(Path.Combine(runtime, "package.json"), default);
        var bun = Root(Path.Combine(runtime, "bun.lock"), BunLockOptions)
            .GetProperty("workspaces").GetProperty("");
        var npm = Root(Path.Combine(runtime, "package-lock.json"), default)
            .GetProperty("packages").GetProperty("");

        Ranges(manifest, "devDependencies").Should().NotBeEmpty("package.json declares the runtime's test tooling");
        foreach (var section in Sections)
        {
            var declared = Ranges(manifest, section);
            Ranges(bun, section).Should().Equal(declared,
                $"bun.lock records package.json's `{section}` as they are; run `bun install` in src/eQuantic.UI.Runtime");
            Ranges(npm, section).Should().Equal(declared,
                $"package-lock.json records package.json's `{section}` as they are; run "
                + "`npm install --package-lock-only` in src/eQuantic.UI.Runtime");
        }
    }

    /// <summary>
    /// <c>bun.lock</c>'s <c>packages</c> map a path to an array whose first element is the resolved
    /// <c>name@version</c>.
    /// </summary>
    private static HashSet<string> BunLockPackages(string path)
    {
        var packages = Root(path, BunLockOptions).GetProperty("packages");
        var resolved = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in packages.EnumerateObject())
        {
            var spec = entry.Value[0].GetString();
            spec.Should().NotBeNullOrEmpty($"bun.lock's entry \"{entry.Name}\" names what it resolved");
            resolved.Add(spec!);
        }
        return resolved;
    }

    /// <summary>
    /// <c>package-lock.json</c>'s <c>packages</c> map an install path to its <c>version</c>; the name is
    /// the path after its last <c>node_modules/</c>, unless the entry is an alias that records its own.
    /// The empty key is the project itself, and a link installs nothing of its own.
    /// </summary>
    private static HashSet<string> PackageLockPackages(string path)
    {
        const string Folder = "node_modules/";
        var packages = Root(path, default).GetProperty("packages");
        var resolved = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in packages.EnumerateObject())
        {
            if (entry.Name.Length == 0
                || (entry.Value.TryGetProperty("link", out var link) && link.GetBoolean()))
                continue;

            var name = entry.Value.TryGetProperty("name", out var alias)
                ? alias.GetString()!
                : entry.Name[(entry.Name.LastIndexOf(Folder, StringComparison.Ordinal) + Folder.Length)..];
            resolved.Add($"{name}@{entry.Value.GetProperty("version").GetString()}");
        }
        return resolved;
    }

    /// <summary>A section's ranges by name, empty when the section is absent.</summary>
    private static List<KeyValuePair<string, string>> Ranges(JsonElement owner, string section) =>
        owner.TryGetProperty(section, out var dependencies)
            ? dependencies.EnumerateObject()
                .Select(dependency => KeyValuePair.Create(dependency.Name, dependency.Value.GetString()!))
                .OrderBy(dependency => dependency.Key, StringComparer.Ordinal)
                .ToList()
            : [];

    private static JsonElement Root(string path, JsonDocumentOptions options)
    {
        File.Exists(path).Should().BeTrue($"{path} is one of the runtime's lockfiles or its manifest");
        using var document = JsonDocument.Parse(File.ReadAllText(path), options);
        return document.RootElement.Clone();
    }

    private static string RuntimeDirectory()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "src", "eQuantic.UI.Runtime")))
            here = here.Parent;
        here.Should().NotBeNull("the suite runs inside the repository, whose root holds src/eQuantic.UI.Runtime");
        return Path.Combine(here!.FullName, "src", "eQuantic.UI.Runtime");
    }
}
