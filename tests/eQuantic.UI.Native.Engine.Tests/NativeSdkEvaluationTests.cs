using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What the native SDK actually RESOLVES for a consumer, asked of MSBuild rather than of the XML.
///
/// <para>
/// Its sibling <see cref="NativeSdkPlatformQuestionTests"/> is a structural guard: it says where the
/// platform question may be asked. Structure is not behaviour — delete the block that turns the
/// Xcode comparison off, or flip its value, and that guard stays green, because the rule it knows
/// is "no PropertyGroup in Sdk.props reads the platform" and deleting one never breaks it. So this
/// evaluates a throwaway consumer of each shape and pins the answers themselves.
/// </para>
/// <para>
/// The shape that matters is the one this repository does not contain: a body-declared
/// <c>&lt;TargetFramework&gt;net10.0-ios&lt;/TargetFramework&gt;</c>. WalletMobile is multi-target and
/// the native template is desktop-only, so every iOS default went unexercised until an app of that
/// shape was built by hand — it took the macOS desktop shell, a platform minimum of whatever iOS SDK
/// compiled it, and the Xcode version check that the SDK means to leave alone.
/// </para>
/// <para>
/// EVALUATION, not a build: <c>-getProperty</c>/<c>-getItem</c> answer from the project graph in a
/// couple of seconds and need no restore, no compiler and no device. The desktop shape needs
/// nothing installed and so runs on every runner; the iOS one needs the ios workload and says so
/// when it is missing rather than reporting a wrong answer.
/// </para>
/// </summary>
public class NativeSdkEvaluationTests
{
    private static string SdkDir([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", "..",
            "src", "eQuantic.UI.Sdk.Native", "Sdk"));

    /// <summary>
    /// The two .NET diagnostics that both mean "this runner cannot evaluate that target framework":
    /// the workload is not installed (1147), or its platform is not even known here because the
    /// manifest that would name it is absent (1139). Named one by one rather than matched loosely —
    /// a fence wide enough to swallow a real failure is how a suite goes quietly green.
    /// </summary>
    private static readonly string[] WorkloadMissing = ["NETSDK1147", "NETSDK1139"];

    /// <summary>
    /// A consumer of the SDK with <paramref name="targetFramework"/> written in the BODY, which is
    /// the whole point: imported above it, Sdk.props cannot see it.
    /// </summary>
    private static string WriteConsumer(string targetFramework)
    {
        var dir = Path.Combine(Path.GetTempPath(), "eq-sdk-eval-" + Guid.NewGuid().ToString("n")[..12]);
        Directory.CreateDirectory(dir);
        var sdk = SdkDir();
        File.WriteAllText(Path.Combine(dir, "EvalConsumer.csproj"), $"""
            <Project>
                <Import Project="{Path.Combine(sdk, "Sdk.props")}" />
                <PropertyGroup>
                    <TargetFramework>{targetFramework}</TargetFramework>
                    <OutputType>Exe</OutputType>
                    <ApplicationId>tech.equantic.eval</ApplicationId>
                    <ApplicationTitle>Eval</ApplicationTitle>
                </PropertyGroup>
                <Import Project="{Path.Combine(sdk, "Sdk.targets")}" />
            </Project>
            """);
        return dir;
    }

    private sealed record Evaluation(JsonElement Properties, JsonElement Items, string Raw)
    {
        public string Property(string name) =>
            Properties.TryGetProperty(name, out var value) ? value.GetString() ?? "" : "";

        public IEnumerable<string> Identities(string item) =>
            Items.TryGetProperty(item, out var entries)
                ? entries.EnumerateArray().Select(e => e.GetProperty("Identity").GetString() ?? "")
                : [];
    }

    /// <summary>
    /// Asks MSBuild, and skips ONLY for a missing workload — with the target framework named, so a
    /// skip reads as "this runner cannot answer" and never as "the answer was fine".
    /// </summary>
    private static Evaluation Evaluate(string targetFramework, params string[] extraArgs)
    {
        var dir = WriteConsumer(targetFramework);
        try
        {
            var args = new List<string>
            {
                "msbuild", Path.Combine(dir, "EvalConsumer.csproj"), "-nologo",
                "-getProperty:_EqPlatform", "-getProperty:ValidateXcodeVersion",
                "-getProperty:SupportedOSPlatformVersion", "-getProperty:_EqDesktopShell",
                "-getProperty:_EqMacHead",
                "-getItem:ProjectReference", "-getItem:PackageReference",
                "-getItem:PartialAppManifest", "-getItem:TrimmerRootAssembly",
            };
            args.AddRange(extraArgs);

            var info = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = dir,
            };
            foreach (var arg in args)
                info.ArgumentList.Add(arg);

            var process = Process.Start(info)
                ?? throw new InvalidOperationException(
                    "`dotnet` did not start. This suite runs on .NET, so the SDK is installed — " +
                    "it is not on this process's PATH.");

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(milliseconds: 180_000).Should().BeTrue(
                $"evaluating a {targetFramework} consumer must not hang");

            var output = stdout + stderr;
            var absent = WorkloadMissing
                .FirstOrDefault(code => output.Contains(code, StringComparison.Ordinal));
            Skip.If(absent is not null,
                $"This {RuntimeOs()} runner cannot evaluate a {targetFramework} project ({absent}): " +
                $"its workload is not installed. `dotnet workload install ios android` is what the " +
                $"macOS CI job runs before the suite, which is where this assertion lands.");

            process.ExitCode.Should().Be(0,
                $"evaluating a {targetFramework} consumer of the native SDK must succeed.\n{output}");

            using var document = JsonDocument.Parse(stdout);
            var root = document.RootElement.Clone();
            return new Evaluation(
                root.GetProperty("Properties"),
                root.TryGetProperty("Items", out var items) ? items : default,
                output);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { /* a temp dir */ }
        }
    }

    private static string RuntimeOs() =>
        OperatingSystem.IsMacOS() ? "macOS" : OperatingSystem.IsWindows() ? "Windows" : "Linux";

    private static string Shell(Evaluation evaluation) =>
        evaluation.Identities("ProjectReference").Concat(evaluation.Identities("PackageReference"))
            .Select(identity => Path.GetFileNameWithoutExtension(identity.Replace('\\', '/')))
            .SingleOrDefault(name => name.Contains("Shell", StringComparison.Ordinal))
        ?? "NONE";

    /// <summary>
    /// The row that was wrong. Every value here is the one a multi-target iOS leg already got; the
    /// defect was that this shape got a different one, silently.
    /// </summary>
    [SkippableFact]
    public void ASingleTargetIosApp_ResolvesAsIos()
    {
        var ios = Evaluate("net10.0-ios");

        ios.Property("_EqPlatform").Should().Be("ios",
            "an app whose target framework names iOS is an iOS app, whether it says so in the body " +
            "or through a global property");
        ios.Property("ValidateXcodeVersion").Should().Be("false",
            "the SDK does not dictate which Xcode the developer has — and this is the shape where " +
            "that default was missed, so it is the shape worth pinning");
        ios.Property("SupportedOSPlatformVersion").Should().Be("15.0",
            "15.0 is the SDK's minimum; without it the app silently takes the iOS SDK's own, and " +
            "refuses to install on anything older than the machine that built it");
        ios.Property("_EqMacHead").Should().BeEmpty("an iPhone app is not a macOS head");

        Shell(ios).Should().Be("eQuantic.UI.Native.Shell.iOS");
        ios.Identities("PartialAppManifest").Select(Path.GetFileName)
            .Should().Contain("PhotonApp.plist",
                "without UILaunchScreen iOS runs the app in a compatibility window and every " +
                "safe-area inset it reports belongs to that fiction");
        ios.Identities("TrimmerRootAssembly")
            .Should().Contain("eQuantic.UI.Native.Shell.iOS",
                "nothing references the shell in code, so the trimmer removes it unless it is rooted");
    }

    /// <summary>
    /// The escape hatch, which is what makes the default a default rather than a decision taken for
    /// the developer: their own value survives.
    /// </summary>
    [SkippableFact]
    public void TheDeveloperCanAskForTheXcodeCheckBack()
    {
        Evaluate("net10.0-ios", "-p:ValidateXcodeVersion=true")
            .Property("ValidateXcodeVersion").Should().Be("true",
                "the SDK only fills this in when it is empty");
    }

    /// <summary>
    /// The shape that always worked, pinned because the fix moved the code that decides it: a head
    /// with no target platform is still the desktop one, resolved from the HOST rather than named
    /// by the project.
    /// </summary>
    [SkippableFact]
    public void ADesktopAppStillResolvesFromTheHost()
    {
        var desktop = Evaluate("net10.0");

        desktop.Property("_EqPlatform").Should().BeEmpty("no target platform is the desktop head");
        desktop.Property("ValidateXcodeVersion").Should().BeEmpty(
            "the iOS default must not leak to a target framework that never sees Xcode");

        var expected = OperatingSystem.IsWindows() ? "Windows" : "MacOS";
        desktop.Property("_EqDesktopShell").Should().Be(expected,
            "which desktop is the machine's to say, not the project's");
        Shell(desktop).Should().Be($"eQuantic.UI.Native.Shell.{expected}");
    }
}
