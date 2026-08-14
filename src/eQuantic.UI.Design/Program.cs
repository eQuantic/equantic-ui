using System.Text;
using System.Text.Json;
using eQuantic.UI.Design;

// STDOUT IS THE PROTOCOL. The compiler and the helpers it calls write progress to Console.Out, and
// a single stray line of prose in the middle of the stream desynchronises the reader for the rest of
// the session. So the console is redirected to stderr — where the extension surfaces it as a log —
// and the real stdout is kept privately for responses.
var protocol = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = false };
Console.SetOut(Console.Error);

var input = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));
var session = new DesignSession();
var json = new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

while (await input.ReadLineAsync() is { } line)
{
    if (line.Length == 0) continue;

    DesignRequest? request = null;
    DesignResponse response;
    try
    {
        request = JsonSerializer.Deserialize<DesignRequest>(line, json)
                  ?? throw new InvalidOperationException("empty request");

        var parameters = request.Params ?? new DesignParams();
        object result = request.Method switch
        {
            "initialize" => session.Initialize(
                Require(parameters.ProjectDir, "projectDir"),
                parameters.RefsFile,
                parameters.GeneratedDir),

            "diagnose" => session.Diagnose(
                Require(parameters.Path, "path"),
                parameters.Text ?? ""),

            "compile" => session.Compile(
                Require(parameters.Path, "path"),
                parameters.Text ?? ""),

            "theme" => session.Theme(),

            "shutdown" => "bye",

            _ => throw new InvalidOperationException($"unknown method '{request.Method}'"),
        };

        response = new DesignResponse { Id = request.Id, Result = result };
    }
    catch (Exception ex)
    {
        // Every failure answers the request it belongs to. A host that simply stops replying leaves
        // the editor waiting forever on a promise, which reads as "the preview hung".
        response = new DesignResponse { Id = request?.Id ?? 0, Error = ex.Message };
    }

    await protocol.WriteLineAsync(JsonSerializer.Serialize(response, json));
    await protocol.FlushAsync();

    if (request?.Method == "shutdown") break;
}

return 0;

static string Require(string? value, string name) =>
    string.IsNullOrEmpty(value) ? throw new InvalidOperationException($"'{name}' is required") : value;
