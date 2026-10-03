using System.Text;
using System.Text.Json;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The client configuration a served document carries as <c>window.__EQ_CONFIG</c>, parsed as the
/// JSON it is: the first value after the assignment, read by a JSON reader that stops where the value
/// ends. Reading it as JSON is the assertion that it is one.
/// </summary>
internal static class ShellConfig
{
    private const string Assignment = "window.__EQ_CONFIG = ";

    public static JsonElement In(string html)
    {
        var start = html.IndexOf(Assignment, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0, "every page carries the client's configuration");
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(html[(start + Assignment.Length)..]),
            new JsonReaderOptions { AllowTrailingCommas = false });
        return JsonDocument.ParseValue(ref reader).RootElement.Clone();
    }
}
