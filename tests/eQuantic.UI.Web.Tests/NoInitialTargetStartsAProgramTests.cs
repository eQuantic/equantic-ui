using System.Xml.Linq;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// No MSBuild file the repository ships starts a program, or builds a project, from an InitialTarget
/// (#1135). An InitialTarget runs in EVERY invocation of the project: the Android workload's device
/// listing and its deploy, a restore, a design-time build, each inner build. The Android icon was one, so
/// listing devices built the eqicon edge and started eqicon, and on a source tree, where `dotnet run`
/// starts those invocations with no DOTNET_HOST_PATH, the task host could not start and the listing
/// failed. Read from the targets each invocation runs: an InitialTarget, and every target it depends on
/// in the file that declares it.
/// </summary>
public class NoInitialTargetStartsAProgramTests
{
    /// <summary>The tasks that start a program or build a project.</summary>
    private static readonly HashSet<string> Starting = ["RunProgram", "Exec", "MSBuild", "CallTarget"];

    private static List<string> ShippedMsBuildFiles(string src) =>
        Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".props", StringComparison.Ordinal) || path.EndsWith(".targets", StringComparison.Ordinal))
            .Where(path => !Path.GetRelativePath(src, path).Split(Path.DirectorySeparatorChar)
                .Any(segment => segment is "bin" or "obj" or "node_modules"))
            .ToList();

    [Fact]
    public void NoInitialTarget_StartsAProgramOrBuildsAProject()
    {
        var src = Path.Combine(SdkProbe.RepoRoot(), "src");
        var files = ShippedMsBuildFiles(src);
        files.Should().Contain(path => path.EndsWith(Path.Combine("eQuantic.UI.Sdk.Native", "Sdk", "Sdk.targets"), StringComparison.Ordinal),
            "the scan reads the native SDK's own targets, or it proves nothing");

        var offenders = new List<string>();
        foreach (var path in files)
        {
            var project = XDocument.Load(path).Root!;
            var targets = project.Elements().Where(element => element.Name.LocalName == "Target")
                .GroupBy(target => (string)target.Attribute("Name")!)
                .ToDictionary(group => group.Key, group => group.ToList());
            var pending = new Stack<string>(Names((string?)project.Attribute("InitialTargets")));
            var seen = new HashSet<string>();
            while (pending.TryPop(out var name))
            {
                if (!seen.Add(name) || !targets.TryGetValue(name, out var declared)) continue;
                foreach (var target in declared)
                {
                    offenders.AddRange(target.Descendants()
                        .Where(task => Starting.Contains(task.Name.LocalName))
                        .Select(task => $"{Path.GetRelativePath(src, path)}: {name} runs {task.Name.LocalName}"));
                    foreach (var dependency in Names((string?)target.Attribute("DependsOnTargets")))
                        pending.Push(dependency);
                }
            }
        }

        offenders.Should().BeEmpty("an InitialTarget runs in every invocation of the project, a device listing and a deploy "
            + "included, so none starts a program or builds a project");
    }

    /// <summary>
    /// The Android icon is written just before Android collects its resources, where a declared resource
    /// that does not exist is an XA2001 (<c>AndroidComputeResPaths</c>, in <c>_ComputeAndroidResourcePaths</c>).
    /// </summary>
    [Fact]
    public void TheAndroidIcon_IsWrittenBeforeAndroidCollectsItsResources()
    {
        var icon = NativeTargets().Single(target => (string?)target.Attribute("Name") == "EQuanticAndroidIcon");

        Names((string?)icon.Attribute("BeforeTargets")).Should().Contain("_ComputeAndroidResourcePaths");
    }

    /// <summary>
    /// Every Android target of the native SDK that starts a program runs only when what it reads changed,
    /// since the workload's deploy walks the build's targets again: after a build, a deploy starts no tool.
    /// The queries fragment started eqicon on every deploy, and the icon, as an InitialTarget, on every
    /// invocation (#1135).
    /// </summary>
    [Fact]
    public void AnAndroidTargetThatStartsAProgram_RunsOnlyWhenItIsOutOfDate()
    {
        var android = NativeTargets()
            .Where(target => ((string?)target.Attribute("Condition") ?? "").Contains("'android'", StringComparison.Ordinal))
            .Where(target => target.Descendants().Any(task => Starting.Contains(task.Name.LocalName)))
            .ToList();

        android.Select(target => (string?)target.Attribute("Name"))
            .Should().Contain(["EQuanticAndroidIcon", "EQuanticAndroidQueries"], "the scan reads the targets it is about");
        android.Where(target => string.IsNullOrWhiteSpace((string?)target.Attribute("Inputs"))
                || string.IsNullOrWhiteSpace((string?)target.Attribute("Outputs")))
            .Select(target => (string?)target.Attribute("Name"))
            .Should().BeEmpty("a deploy after a build finds every Android target in date and starts no tool");
    }

    private static IEnumerable<XElement> NativeTargets() =>
        XDocument.Load(Path.Combine(SdkProbe.RepoRoot(), "src", "eQuantic.UI.Sdk.Native", "Sdk", "Sdk.targets")).Root!
            .Elements().Where(element => element.Name.LocalName == "Target");

    private static IEnumerable<string> Names(string? list) =>
        (list ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
