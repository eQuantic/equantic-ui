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
/// EVALUATION, not a build: <c>-getProperty</c>/<c>-getItem</c> answer from the project graph in
/// under a second each, with no restore, no compiler and no device. Every value asserted below is
/// one THIS SDK decides, so none of it needs the platform's workload installed — measured, these
/// run and pass on all three runners, including the Linux and Windows legs that have no iOS or
/// Android workload at all. An earlier revision fenced them behind a skip for a missing workload;
/// evaluation never emits those diagnostics (confirmed against tvos and maccatalyst, whose packs
/// are absent here and which evaluate cleanly), so the fence was a branch that could not run and
/// could only ever have swallowed a real failure.
/// </para>
/// </summary>
public class NativeSdkEvaluationTests
{
    private static string RepoRoot([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    private static string SdkDir() =>
        Path.Combine(RepoRoot(), "src", "eQuantic.UI.Sdk.Native", "Sdk");

    /// <summary>
    /// Long enough that a cold evaluation on a loaded runner never trips it, short enough that a
    /// hung MSBuild fails this test rather than the whole job's time budget.
    /// </summary>
    private static readonly TimeSpan EvaluationTimeout = TimeSpan.FromMinutes(3);

    /// <summary>
    /// A consumer of the SDK with <paramref name="targetFramework"/> written in the BODY, which is
    /// the whole point: imported above it, Sdk.props cannot see it.
    /// </summary>
    private static string WriteConsumer(string targetFramework, string bodyProperties)
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
                    {bodyProperties}
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

    /// <summary>Asks MSBuild, and fails loudly with the child's own output when it cannot.</summary>
    private static Evaluation Evaluate(string targetFramework, string bodyProperties = "")
    {
        var dir = WriteConsumer(targetFramework, bodyProperties);
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

            var info = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                // The REPOSITORY root, not the throwaway project's folder: `dotnet` picks its SDK
                // from the global.json it finds by walking up from the working directory, and this
                // repository pins one. Run from a temp folder and the child evaluates with whatever
                // SDK the machine happens to default to, which makes the answers below depend on
                // the machine rather than on the tree.
                WorkingDirectory = RepoRoot(),
            };
            foreach (var arg in args)
                info.ArgumentList.Add(arg);

            var process = Process.Start(info)
                ?? throw new InvalidOperationException(
                    "`dotnet` did not start. This suite runs on .NET, so the SDK is installed — " +
                    "it is not on this process's PATH.");

            // Both pipes drain from the start, and the wait is what enforces the deadline. Reading
            // one stream to the end before touching the other deadlocks the moment the child fills
            // the pipe nobody is reading — and a deadline checked only after both reads returned is
            // not a deadline at all: it can never fire on the hang it exists for.
            var outText = process.StandardOutput.ReadToEndAsync();
            var errText = process.StandardError.ReadToEndAsync();

            using var deadline = new CancellationTokenSource(EvaluationTimeout);
            try
            {
                process.WaitForExitAsync(deadline.Token).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { /* it exited between the timeout and the kill */ }

                throw new TimeoutException(
                    $"evaluating a {targetFramework} consumer did not finish within " +
                    $"{EvaluationTimeout}; the process tree was killed.");
            }

            var stdout = outText.GetAwaiter().GetResult();
            var stderr = errText.GetAwaiter().GetResult();

            var output = stdout + stderr;
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

    private static string Shell(Evaluation evaluation) =>
        evaluation.Identities("ProjectReference").Concat(evaluation.Identities("PackageReference"))
            .Select(identity => Path.GetFileNameWithoutExtension(identity.Replace('\\', '/')))
            .SingleOrDefault(name => name.Contains("Shell", StringComparison.Ordinal))
        ?? "NONE";

    /// <summary>
    /// The row that was wrong. Every value here is the one a multi-target iOS leg already got; the
    /// defect was that this shape got a different one, silently.
    /// </summary>
    [Fact]
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
    /// Android's twin. The fix moved this platform's minimum too, and nothing else here would
    /// notice it going: the multi-target sample proves only that an APK comes out, never which
    /// minimum it carries.
    /// </summary>
    [Fact]
    public void ASingleTargetAndroidApp_ResolvesAsAndroid()
    {
        var android = Evaluate("net10.0-android");

        android.Property("_EqPlatform").Should().Be("android");
        android.Property("SupportedOSPlatformVersion").Should().Be("26.0",
            "26 is where adaptive icons, the Choreographer's frame callback and Vulkan all " +
            "arrived; without it the app takes the Android SDK's own minimum");
        android.Property("ValidateXcodeVersion").Should().BeEmpty(
            "the iOS default must not leak to a platform that has never heard of Xcode");
        android.Property("_EqMacHead").Should().BeEmpty("an Android app is not a macOS head");

        Shell(android).Should().Be("eQuantic.UI.Native.Shell.Android");
        android.Identities("TrimmerRootAssembly")
            .Should().Contain("eQuantic.UI.Native.Shell.Android");
    }

    /// <summary>
    /// The escape hatch, which is what makes the default a default rather than a decision taken for
    /// the developer: their own value survives.
    ///
    /// <para>
    /// From the project BODY, which is the case the SDK's comment promises and the only one that
    /// can fail. Passing it as a global property (`-p:`) proves nothing here: MSBuild refuses to
    /// let any file overwrite a global, so that assertion would pass even if this SDK assigned the
    /// property unconditionally — it would be testing MSBuild. The body is the real test, because
    /// Sdk.targets is imported BELOW it and could overwrite it.
    /// </para>
    /// </summary>
    [Fact]
    public void TheDeveloperCanAskForTheXcodeCheckBack()
    {
        Evaluate("net10.0-ios", "<ValidateXcodeVersion>true</ValidateXcodeVersion>")
            .Property("ValidateXcodeVersion").Should().Be("true",
                "the SDK only fills this in when it is empty, so an app that states its own keeps it");
    }

    /// <summary>
    /// The shape that always worked, pinned because the fix moved the code that decides it: a head
    /// with no target platform is still the desktop one, resolved from the HOST rather than named
    /// by the project.
    /// </summary>
    [Fact]
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
