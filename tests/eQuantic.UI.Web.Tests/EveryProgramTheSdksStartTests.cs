using System.IO.Compression;
using System.Xml.Linq;
using eQuantic.UI.Sdk.Tasks;
using FluentAssertions;
using static eQuantic.UI.Web.Tests.SdkProbe;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// What every program the two SDKs start is handed (#841): their targets, taken from the
/// <c>Sdk.targets</c> each package ships, run in a real MSBuild under a folder whose name holds what a
/// shell reads, with a recorder on the PATH in place of every program. MSBuild's <c>Exec</c> wrote each
/// command into a script for <c>/bin/sh</c>, and measured with the package folder under a folder named
/// <c>nuget $b</c>, a build stopped at the first program it started. The values here are the ones a
/// developer or a CI writes: a title with a dollar sign, an ampersand, quotes and a trailing backslash,
/// a signing identity with spaces and parentheses, a notary's password with quotes in it.
/// </summary>
public class EveryProgramTheSdksStartTests
{
    /// <summary>A folder name with everything a shell reads, which macOS and Linux take whole.</summary>
    private const string Odd = "eq $args `c` 'd' |e \"f\"";

    /// <summary>The app's title, which reaches the icon tool as an argument.</summary>
    private const string Title = "Cash$Flow & Co \"Ltd\" C:\\";

    /// <summary>
    /// The title a macOS bundle's files are named by. MSBuild itself turns a backslash into a slash in
    /// a property that holds a path on macOS and Linux, with or without a shell, so a title that ends
    /// with one is no bundle name to measure the arguments by.
    /// </summary>
    private const string AppName = "Cash$Flow & Co \"Ltd\"";

    private static string Bun => OperatingSystem.IsMacOS() ? "bun-darwin" : "bun-linux";

    private static XElement Properties(params (string Name, string Value)[] properties) =>
        new("PropertyGroup", properties.Select(property => new XElement(property.Name, property.Value)));

    private static XElement StandIn(string target) => new("Target", new XAttribute("Name", target));

    /// <summary>A probe folder under <see cref="Odd"/>, the recorders, and the PATH that finds them first.</summary>
    private static (string Directory, Dictionary<string, string> Environment, string Record) Prepare(string name, params string[] programs)
    {
        var root = Path.Combine(RealPath(Directory.CreateTempSubdirectory("eq-programs-").FullName), Odd);
        var directory = Path.Combine(root, name);
        Directory.CreateDirectory(directory);
        File.Copy(Path.Combine(RepoRoot(), "global.json"), Path.Combine(root, "global.json"));
        var recorders = Path.Combine(root, "recorders");
        Recorders(recorders, programs);
        var record = Path.Combine(root, "record.bin");
        return (directory, new Dictionary<string, string>
        {
            ["PATH"] = recorders + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"),
            ["EQ_RECORD"] = record,
        }, record);
    }

    /// <summary>
    /// <paramref name="path"/> with every link in it followed: macOS keeps its temporary folders under
    /// <c>/var</c>, a link to <c>/private/var</c>, and MSBuild started inside one names the real path.
    /// </summary>
    private static string RealPath(string path)
    {
        var real = Path.GetPathRoot(path)!;
        foreach (var segment in path[real.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            real = Path.Combine(real, segment);
            if (new DirectoryInfo(real).ResolveLinkTarget(returnFinalTarget: true) is { } target) real = target.FullName;
        }
        return real;
    }

    private static void Clean(string directory) =>
        Directory.Delete(Path.GetDirectoryName(Path.GetDirectoryName(directory)!)!, recursive: true);

    [UnixFact]
    public void TheWebSdk_HandsEachProgramItsArguments_AsTheyAre()
    {
        var shipped = Shipped("eQuantic.UI.Sdk");
        var (directory, environment, record) = Prepare("App", "dotnet", "git");
        var framework = Path.Combine(Path.GetDirectoryName(directory)!, "framework $f");
        var runtime = Path.Combine(directory, "runtime $r");
        // The bun ships zipped, and the build unpacks it into the user's cache, never beside the zip
        // (#364): a home of the probe's own, so the developer's cache is not written. dotnet keeps its own.
        var stand = Path.Combine(directory, "stand-in");
        Recorders(stand, Bun);
        Directory.CreateDirectory(runtime);
        using (var zip = ZipFile.Open(Path.Combine(runtime, Bun + ".zip"), ZipArchiveMode.Create))
            zip.CreateEntryFromFile(Path.Combine(stand, Bun), Bun);
        var home = Path.Combine(Path.GetDirectoryName(directory)!, "home $h");
        environment["DOTNET_CLI_HOME"] = Environment.GetEnvironmentVariable("DOTNET_CLI_HOME") ?? Environment.GetEnvironmentVariable("HOME")!;
        environment["HOME"] = home;
        environment["XDG_CACHE_HOME"] = Path.Combine(home, ".cache");
        Directory.CreateDirectory(Path.Combine(directory, "Assets $a"));
        File.WriteAllText(Path.Combine(directory, "Assets $a", "mark.svg"), "<svg/>");
        try
        {
            var probe = Path.Combine(directory, "probe.proj");
            new XDocument(new XElement("Project",
                Properties(
                    ("_EqSourceTree", "false"),
                    ("_EqTasksAssembly", typeof(RunProgram).Assembly.Location),
                    ("IsEQuanticDevMode", "true"),
                    ("_DevRootDir", framework + "/src/"),
                    ("EnableEQuanticUICompilation", "true"),
                    ("BaseIntermediateOutputPath", "$(MSBuildProjectDirectory)/obj/"),
                    ("IntermediateOutputPath", "obj/"),
                    ("EQuanticOutputPath", "wwwroot/_equantic/"),
                    ("_EqcSourceDirs", "$(MSBuildProjectDirectory)"),
                    ("_EqcRefsFile", "$(MSBuildProjectDirectory)/obj/equantic.refs.txt"),
                    ("AssemblyName", "Cash$Flow`Back`"),
                    ("EQuanticSourceMaps", "full"),
                    ("EqcCliPath", "$(MSBuildProjectDirectory)/tools $x/eqc.dll"),
                    ("_EqIconToolDir", "$(MSBuildProjectDirectory)/tools $x/"),
                    ("BunZipPath", $"$(MSBuildProjectDirectory)/runtime $r/{Bun}.zip"),
                    ("EQuanticAssetsDir", "$(MSBuildProjectDirectory)/Assets $a"),
                    ("EQuanticVectorsFile", "Vectors.g.cs"),
                    ("EQuanticVectorsNamespace", "App.Vectors"),
                    ("EQuanticAppIconSource", "$(MSBuildProjectDirectory)/icon `i`.png"),
                    ("ApplicationTitle", Title)),
                new XElement("ItemGroup",
                    new XElement("BunPackage", new XAttribute("Include", "dayjs"), new XAttribute("Version", "1.11.13")),
                    new XElement("BunPackage", new XAttribute("Include", "@scope/odd"), new XAttribute("Version", "2.0.0"))),
                UsingTasks(shipped),
                StandIn("ResolveBunZipPath"),
                StandIn("ResolveBunPath"),
                StandIn("_EQuanticCompilerInputs"),
                Target(shipped, "EQuanticAnnounceDevSource"),
                Target(shipped, "EnsureBunExtracted"),
                Target(shipped, "InstallBunPackages"),
                Target(shipped, "_EQuanticVectorInputs"),
                Target(shipped, "EQuanticVectors"),
                Target(shipped, "EQuanticWebAppIcon"),
                Target(shipped, "_EQuanticCompilerArguments"),
                Target(shipped, "CompileEQuanticUI"))).Save(probe);

            var output = Dotnet(directory, environment, "msbuild", "probe.proj", "-nologo", "-v:m",
                "-t:EQuanticAnnounceDevSource;EQuanticWebAppIcon;CompileEQuanticUI");

            Calls(record).Should().Equal([
                Call("git", "-C", framework, "describe", "--always", "--dirty", "--abbrev=8"),
                Call("dotnet", directory + "/tools $x/eqicon.dll", "--source", directory + "/icon `i`.png",
                    "--web", directory + "/wwwroot/_equantic/icons", "--app", Title),
                Call(Bun, "add", "dayjs@1.11.13"),
                Call(Bun, "add", "@scope/odd@2.0.0"),
                Call("dotnet", directory + "/tools $x/eqicon.dll", "vectors", "--assets", directory + "/Assets $a",
                    "--out", directory + "/Vectors.g.cs", "--namespace", "App.Vectors"),
                Call("dotnet", directory + "/tools $x/eqc.dll", "@" + directory + "/obj/equantic.eqc.rsp"),
            ]);
            output.Should().Contain("@ v1.2.3-4-gabcdef12", "what git answers is read back, as Exec read it");
            Directory.EnumerateFileSystemEntries(runtime).Should().Equal([Path.Combine(runtime, Bun + ".zip")],
                "nothing is unpacked beside the zip, which may sit in a read-only package cache");
            Directory.EnumerateFiles(home, Bun, SearchOption.AllDirectories).Should().ContainSingle()
                .Which.Should().Contain("eQuantic", "the bun that ran is the one unpacked into the user's cache");
            new DirectoryInfo(Path.Combine(directory, "node_modules")).LinkTarget
                .Should().Be(directory + "/obj/eQuantic/npm/node_modules", "the packages are linked where tools look for them");
        }
        finally
        {
            Clean(directory);
        }
    }

    [UnixFact]
    public void EqcIsHandedItsResponseFile_ByItsFullPath_WhereverObjIs()
    {
        // The obj folder's own path holds a Configuration, a target framework or an artifacts
        // layout's folders, and MSBuild writes it with a backslash in it on every system.
        var shipped = Shipped("eQuantic.UI.Sdk");
        foreach (var obj in new[] { "obj\\Rel$ease `x` 'y'/", "$(MSBuildProjectDirectory)/../artifacts $a/obj/" })
        {
            var (directory, environment, record) = Prepare("App", "dotnet");
            try
            {
                new XDocument(new XElement("Project",
                    Properties(
                        ("_EqSourceTree", "false"),
                        ("_EqTasksAssembly", typeof(RunProgram).Assembly.Location),
                        ("EnableEQuanticUICompilation", "true"),
                        ("IntermediateOutputPath", obj),
                        ("EQuanticOutputPath", "wwwroot/_equantic/"),
                        ("_EqcSourceDirs", "$(MSBuildProjectDirectory)"),
                        ("_EqcRefsFile", "$(MSBuildProjectDirectory)/obj/equantic.refs.txt"),
                        ("EQuanticSourceMaps", "none"),
                        ("EqcCliPath", "$(MSBuildProjectDirectory)/tools/eqc.dll")),
                    UsingTasks(shipped),
                    StandIn("_EQuanticCompilerInputs"),
                    StandIn("ResolveBunPath"),
                    Target(shipped, "_EQuanticCompilerArguments"),
                    Target(shipped, "CompileEQuanticUI"))).Save(Path.Combine(directory, "probe.proj"));

                Dotnet(directory, environment, "msbuild", "probe.proj", "-nologo", "-v:m", "-t:CompileEQuanticUI");

                var file = Path.GetFullPath(Path.Combine(directory,
                    obj.Replace("$(MSBuildProjectDirectory)/", "").Replace('\\', '/'), "equantic.eqc.rsp"));
                File.Exists(file).Should().BeTrue($"the response file is written under {obj}");
                Calls(record).Should().Equal([Call("dotnet", directory + "/tools/eqc.dll", "@" + file)],
                    $"eqc is handed the file that was written, with obj at {obj}");
            }
            finally
            {
                Clean(directory);
            }
        }
    }

    [UnixFact]
    public void EveryNpmPackageAnAppDeclares_IsHandedToBun()
    {
        // The install ran in batches under one condition, "no node_modules yet", which MSBuild asks
        // again before each batch. The first `bun add` made the folder, so the second package of an
        // app was never installed: measured on main with a bun that makes the folder, as bun does.
        var shipped = Shipped("eQuantic.UI.Sdk");
        var (directory, environment, record) = Prepare("Packages");
        var bun = Path.Combine(directory, "bun");
        File.WriteAllText(bun, "#!/bin/sh\nmkdir -p node_modules && printf '%s\\n' \"$2\" >> \"$EQ_RECORD\"\n");
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(bun, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try
        {
            new XDocument(new XElement("Project",
                Properties(
                    ("_EqSourceTree", "false"),
                    ("_EqTasksAssembly", typeof(RunProgram).Assembly.Location),
                    ("BunPath", bun),
                    ("BaseIntermediateOutputPath", "$(MSBuildProjectDirectory)/obj/")),
                new XElement("ItemGroup",
                    new XElement("BunPackage", new XAttribute("Include", "dayjs"), new XAttribute("Version", "1.11.13")),
                    new XElement("BunPackage", new XAttribute("Include", "marked"), new XAttribute("Version", "15.0.6"))),
                UsingTasks(shipped),
                StandIn("ResolveBunPath"),
                Target(shipped, "InstallBunPackages"))).Save(Path.Combine(directory, "probe.proj"));

            Dotnet(directory, environment, "msbuild", "probe.proj", "-nologo", "-v:m", "-t:InstallBunPackages");

            File.ReadAllLines(record).Should().Equal(["dayjs@1.11.13", "marked@15.0.6"],
                "every package is added, the ones after the first included");
        }
        finally
        {
            Clean(directory);
        }
    }

    [UnixFact]
    public void AProgramRunsInTheProjectsFolder_WhereverMSBuildWasStarted()
    {
        // MSBuild makes the project's folder the current one while it builds the project, and starts a
        // task host in it. So the folder a step names by a relative path is the project's, however the
        // task is loaded, and so is the one a relative argument is read from. Every other probe here is
        // started inside its own folder, which could not tell (found in review of #855): this one is
        // started in the repository's root, with obj written as a project file writes it.
        var shipped = Shipped("eQuantic.UI.Sdk");
        foreach (var factory in new[] { "AssemblyTaskFactory", "TaskHostFactory" })
        {
            // No double quote in this path: MSBuild's own command line drops one from a project's.
            var root = RealPath(Directory.CreateTempSubdirectory("eq-where $w ").FullName);
            var directory = Path.Combine(root, "App");
            Directory.CreateDirectory(directory);
            var bun = Path.Combine(root, "bun");
            File.WriteAllText(bun, "#!/bin/sh\nmkdir -p node_modules && pwd >> \"$EQ_RECORD\"\n");
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(bun, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var record = Path.Combine(root, "where.txt");
            try
            {
                // The targets in a file of their own, imported, as the SDK's are: the file that holds a
                // task is then not the project, and a folder read from it would be the SDK's.
                Directory.CreateDirectory(Path.Combine(root, "Sdk"));
                new XDocument(new XElement("Project",
                    UsingTasks(shipped),
                    StandIn("ResolveBunPath"),
                    Target(shipped, "InstallBunPackages"))).Save(Path.Combine(root, "Sdk", "Sdk.targets"));
                var probe = Path.Combine(directory, "probe.proj");
                new XDocument(new XElement("Project",
                    Properties(
                        ("_EqSourceTree", "false"),
                        ("_EqTasksAssembly", typeof(RunProgram).Assembly.Location),
                        ("_EqTaskFactory", factory),
                        ("BunPath", bun),
                        ("BaseIntermediateOutputPath", "obj/")),
                    new XElement("ItemGroup",
                        new XElement("BunPackage", new XAttribute("Include", "dayjs"), new XAttribute("Version", "1.11.13"))),
                    new XElement("Import", new XAttribute("Project", "../Sdk/Sdk.targets")))).Save(probe);

                Dotnet(null, new Dictionary<string, string> { ["EQ_RECORD"] = record },
                    "msbuild", probe, "-nologo", "-v:m", "-t:InstallBunPackages");

                File.ReadAllLines(record).Should().Equal([directory + "/obj/eQuantic/npm"],
                    $"bun runs in the project's own obj, the task loaded by {factory}");
                new DirectoryInfo(Path.Combine(directory, "node_modules")).LinkTarget
                    .Should().Be(directory + "/obj/eQuantic/npm/node_modules", "the link leads into the project's own obj");
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [UnixFact]
    public void TheNativeSdk_HandsEachProgramOfAMacBundleItsArguments_AsTheyAre()
    {
        const string identity = "Developer ID Application: A $b (T)";
        const string password = "pa$$ \"word\" `w`";
        var shipped = Shipped("eQuantic.UI.Sdk.Native");
        var (directory, environment, record) = Prepare("Mac", "dotnet", "chmod", "codesign", "ditto", "xcrun", "hdiutil");
        var bin = directory + "/bin";
        Directory.CreateDirectory(Path.Combine(bin, "osx-arm64"));
        File.WriteAllText(Path.Combine(bin, "osx-arm64", "Cash$Flow.dll"), "");
        Directory.CreateDirectory(Path.Combine(directory, "obj", "eQuantic"));
        File.WriteAllText(Path.Combine(directory, "obj", "eQuantic", "Cash$Flow.entitlements"), "<plist/>");
        try
        {
            var probe = Path.Combine(directory, "probe.proj");
            new XDocument(new XElement("Project",
                Properties(
                    ("_EqSourceTree", "false"),
                    ("_EqTasksAssembly", typeof(RunProgram).Assembly.Location),
                    ("_EqMacHead", "true"),
                    ("AssemblyName", "Cash$Flow"),
                    ("ApplicationTitle", AppName),
                    ("EQuanticBundleId", "com.example.cash"),
                    ("Version", "1.2.3"),
                    ("EQuanticSigningIdentity", identity),
                    ("EQuanticAppIconSource", "$(MSBuildProjectDirectory)/icon.png"),
                    ("_EqIconToolDir", "$(MSBuildProjectDirectory)/tools $x/"),
                    ("_EqIconDir", "$(MSBuildProjectDirectory)/obj/eQuantic"),
                    ("TargetDir", "$(MSBuildProjectDirectory)/bin/"),
                    ("TargetFileName", "Cash$Flow.dll"),
                    ("PublishDir", "$(MSBuildProjectDirectory)/bin/publish/"),
                    ("EQuanticNotaryAppleId", "me@example.com"),
                    ("EQuanticNotaryTeamId", "T"),
                    ("EQuanticNotaryPassword", password),
                    ("EQuanticPackageFormat", "Dmg")),
                UsingTasks(shipped),
                Target(shipped, "EQuanticResolveMacPackaging"),
                Target(shipped, "EQuanticMacAppBundle"),
                Target(shipped, "EQuanticMacAppPackage"))).Save(probe);

            var output = Dotnet(directory, environment, "msbuild", "probe.proj", "-nologo", "-v:n",
                "-t:EQuanticMacAppBundle;EQuanticMacAppPackage");

            var tool = directory + "/tools $x/eqicon.dll";
            var icns = directory + "/obj/eQuantic/" + AppName + ".icns";
            var bundle = bin + "/" + AppName + ".app";
            var ship = bin + "/publish/" + AppName + ".app";
            var dmg = bin + "/publish/" + AppName + ".dmg";
            var zip = directory + "/obj/eQuantic/Cash$Flow.notarize.zip";
            var entitlements = directory + "/obj/eQuantic/Cash$Flow.entitlements";
            Calls(record).Should().Equal([
                Call("dotnet", tool, "entitlements", "--assembly", bin + "/Cash$Flow.dll", "--plist", entitlements,
                    "--also", "com.apple.security.cs.disable-library-validation;com.apple.security.cs.allow-jit",
                    "--hardened", "true"),
                Call("dotnet", tool, "--source", directory + "/icon.png", "--macos", icns),
                Call("dotnet", tool, "bundle", "--app", bundle, "--exec", "Cash$Flow", "--name", AppName,
                    "--id", "com.example.cash", "--version", "1.2.3", "--icns", icns,
                    "--capabilities", bin + "/Cash$Flow.dll", "--payload", bin + "/",
                    "--exclude", bin + "/publish/", "--exclude", bin + "/osx-arm64"),
                Call("chmod", "+x", bundle + "/Contents/MacOS/Cash$Flow"),
                Call("codesign", "--force", "--deep", "--sign", identity, "--options", "runtime",
                    "--entitlements", entitlements, "--identifier", "com.example.cash", bundle),
                Call("dotnet", tool, "--source", directory + "/icon.png", "--macos", icns),
                Call("dotnet", tool, "bundle", "--app", ship, "--exec", "Cash$Flow", "--name", AppName,
                    "--id", "com.example.cash", "--version", "1.2.3", "--icns", icns,
                    "--capabilities", bin + "/Cash$Flow.dll", "--payload", bin + "/publish/", "--exclude", dmg),
                Call("chmod", "+x", ship + "/Contents/MacOS/Cash$Flow"),
                Call("codesign", "--force", "--deep", "--sign", identity, "--options", "runtime",
                    "--entitlements", entitlements, "--identifier", "com.example.cash", ship),
                Call("ditto", "-c", "-k", "--keepParent", ship, zip),
                Call("xcrun", "notarytool", "submit", zip, "--apple-id", "me@example.com", "--team-id", "T",
                    "--password", password, "--wait"),
                Call("xcrun", "stapler", "staple", ship),
                Call("hdiutil", "create", "-volname", AppName, "-srcfolder", ship, "-ov", "-format", "UDZO", dmg),
                Call("codesign", "--force", "--sign", identity, dmg),
            ]);
            output.Should().NotContain("pa$$", "the command line that carries the notary's password is not echoed, "
                + "where the password would show quoted, its own quotes escaped");
        }
        finally
        {
            Clean(directory);
        }
    }

    [UnixFact]
    public void TheNativeSdk_HandsTheIconToolItsArguments_OnEveryPlatform()
    {
        var shipped = Shipped("eQuantic.UI.Sdk.Native");
        var (directory, environment, record) = Prepare("Phone", "dotnet");
        Directory.CreateDirectory(Path.Combine(directory, "tools $x"));
        File.WriteAllText(Path.Combine(directory, "tools $x", "eqicon.dll"), "");
        try
        {
            var probe = Path.Combine(directory, "probe.proj");
            new XDocument(new XElement("Project",
                Properties(
                    ("_EqSourceTree", "false"),
                    ("_EqTasksAssembly", typeof(RunProgram).Assembly.Location),
                    ("TargetFramework", "net10.0-ios"),
                    ("ApplicationId", "com.acme.cash-flow"),
                    ("EQuanticAppIconSource", "$(MSBuildProjectDirectory)/icon `i`.png"),
                    ("_EqIconToolDir", "$(MSBuildProjectDirectory)/tools $x/"),
                    ("_EqIconSetDir", "$(MSBuildProjectDirectory)/obj/eQuantic/Assets.xcassets"),
                    ("_EqAssetsGenerated", "$(MSBuildProjectDirectory)/generated $g"),
                    ("_EqCapabilityPlist", "$(MSBuildProjectDirectory)/obj/eQuantic/Capabilities.plist")),
                new XElement("ItemGroup", new XElement("IntermediateAssembly", new XAttribute("Include", "obj/Cash$Flow.dll"))),
                UsingTasks(shipped),
                Target(shipped, "EQuanticAndroidIcon"),
                Target(shipped, "EQuanticCapabilityManifest"),
                Target(shipped, "EQuanticAppIcon"))).Save(probe);

            Dotnet(directory, environment, "msbuild", "probe.proj", "-nologo", "-v:m", "-p:_EqPlatform=ios",
                "-t:EQuanticCapabilityManifest;EQuanticAppIcon");
            Dotnet(directory, environment, "msbuild", "probe.proj", "-nologo", "-v:m", "-p:_EqPlatform=android",
                "-t:EQuanticAndroidIcon");
            Dotnet(directory, environment, "msbuild", "probe.proj", "-nologo", "-v:m", "-p:TargetFramework=",
                "-t:EQuanticAppIcon");

            var tool = directory + "/tools $x/eqicon.dll";
            var icon = directory + "/icon `i`.png";
            Calls(record).Should().Equal([
                Call("dotnet", tool, "capabilities", "--assembly", "obj/Cash$Flow.dll",
                    "--plist", directory + "/obj/eQuantic/Capabilities.plist", "--id", "com.acme.cash-flow"),
                Call("dotnet", tool, "--source", icon, "--out", directory + "/obj/eQuantic/Assets.xcassets"),
                Call("dotnet", tool, "--source", icon, "--android", directory + "/generated $g/android/res"),
                Call("dotnet", tool, "--source", icon, "--android", directory + "/generated $g/android/res"),
            ], "the iOS asset catalog, the Android resources, and every platform's while restoring");
        }
        finally
        {
            Clean(directory);
        }
    }
}
