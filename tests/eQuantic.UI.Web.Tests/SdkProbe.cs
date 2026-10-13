using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// What the cases over the SDKs' MSBuild files share: the targets as the two SDK packages ship them, a
/// real MSBuild to run them in, and stand-in programs that record what each one was handed.
/// </summary>
internal static class SdkProbe
{
    private static readonly TimeSpan Deadline = TimeSpan.FromMinutes(3);

    /// <summary>
    /// The stand-in for a program: it appends its name, how many arguments it was handed and each of
    /// them NUL-terminated to the file <c>EQ_RECORD</c> names, and as git it also answers describe.
    /// </summary>
    private const string Recorder = """
        #!/bin/sh
        { printf '%s\n%s\n' "${0##*/}" "$#"; for argument in "$@"; do printf '%s\0' "$argument"; done; } >> "$EQ_RECORD"
        if [ "${0##*/}" = git ]; then echo "v1.2.3-4-gabcdef12"; fi
        exit 0

        """;

    /// <summary>The repository's root, from this file's place in it.</summary>
    public static string RepoRoot([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    /// <summary>The Sdk.targets <paramref name="sdkProject"/> ships, as XML.</summary>
    public static XDocument Shipped(string sdkProject) =>
        XDocument.Load(Path.Combine(RepoRoot(), "src", sdkProject, "Sdk", "Sdk.targets"));

    /// <summary>A copy of the target named <paramref name="name"/> in <paramref name="shipped"/>.</summary>
    public static XElement Target(XDocument shipped, string name) =>
        new(shipped.Root!.Elements("Target").Single(target => (string?)target.Attribute("Name") == name));

    /// <summary>Copies of the task declarations <paramref name="shipped"/> makes.</summary>
    public static IEnumerable<XElement> UsingTasks(XDocument shipped) =>
        shipped.Root!.Elements("UsingTask").Select(declaration => new XElement(declaration));

    /// <summary>
    /// Runs <c>dotnet</c> in <paramref name="workingDirectory"/>, the repository's root when it is null,
    /// with <paramref name="environment"/> over its own, and fails with the child's output, which it
    /// returns. Its arguments go to it as a list, never through a shell. MSBuild's own command line drops
    /// every double quote of a project's path, so a probe under a folder with one is run from inside it,
    /// by its name, and the folder carries a copy of the repository's global.json.
    /// </summary>
    public static string Dotnet(string? workingDirectory, IReadOnlyDictionary<string, string>? environment, params string[] args)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory ?? RepoRoot(),
        };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        foreach (var (name, value) in environment ?? new Dictionary<string, string>()) info.Environment[name] = value;
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(Deadline);
        try
        {
            process.WaitForExitAsync(deadline.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { /* it exited between the deadline and the kill */ }
            throw new TimeoutException($"dotnet {string.Join(' ', args)} did not finish within {Deadline}.");
        }
        process.ExitCode.Should().Be(0, output.Result + error.Result);
        return output.Result;
    }

    /// <summary>
    /// Writes the recorder into <paramref name="directory"/> under each of <paramref name="programs"/>,
    /// so a PATH that starts there finds it in their place.
    /// </summary>
    public static void Recorders(string directory, params string[] programs)
    {
        Directory.CreateDirectory(directory);
        foreach (var program in programs)
        {
            var path = Path.Combine(directory, program);
            File.WriteAllText(path, Recorder);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    /// <summary>
    /// The calls the recorders wrote into <paramref name="record"/>, in order, one line each: the
    /// program's name and every argument in brackets, so a failure shows both lists whole.
    /// </summary>
    public static List<string> Calls(string record)
    {
        var bytes = File.ReadAllBytes(record);
        var calls = new List<string>();
        var at = 0;
        string Until(byte end)
        {
            var start = at;
            while (bytes[at] != end) at++;
            return Encoding.UTF8.GetString(bytes, start, at++ - start);
        }
        while (at < bytes.Length)
        {
            var program = Until((byte)'\n');
            var count = int.Parse(Until((byte)'\n'));
            calls.Add(Call(program, [.. Enumerable.Range(0, count).Select(_ => Until(0))]));
        }
        return calls;
    }

    /// <summary>One call as <see cref="Calls"/> writes it.</summary>
    public static string Call(string program, params string[] arguments) =>
        $"{program}: {string.Join(" ", arguments.Select(argument => $"[{argument}]"))}";
}
