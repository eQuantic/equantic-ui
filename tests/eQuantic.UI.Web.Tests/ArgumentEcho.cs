using System.Text.Json;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// This assembly's entry point, which no test run calls. Started as a program,
/// <c>dotnet eQuantic.UI.Web.Tests.dll …</c>, it writes back each argument it received as one JSON
/// string a line, after a blank line when <c>EQ_ECHO_BLANK</c> is set, and ends with the exit code
/// <c>EQ_ECHO_EXIT</c> names. So a case can compare what a program was handed with what it was meant
/// to be, on every system: a .NET program splits its command line on Windows by the convention every
/// Windows program uses, and .NET does it for it on macOS and Linux. The test SDK's generated entry
/// point is off for it (<c>GenerateProgramFile</c>).
/// </summary>
public static class ArgumentEcho
{
    /// <summary>Writes each argument back, and ends with the exit code asked for.</summary>
    public static int Main(string[] args)
    {
        if (Environment.GetEnvironmentVariable("EQ_ECHO_BLANK") is not null) System.Console.WriteLine();
        foreach (var argument in args) System.Console.WriteLine(JsonSerializer.Serialize(argument));
        return int.TryParse(Environment.GetEnvironmentVariable("EQ_ECHO_EXIT"), out var code) ? code : 0;
    }
}
