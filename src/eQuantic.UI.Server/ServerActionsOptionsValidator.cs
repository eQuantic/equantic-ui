using Microsoft.Extensions.Options;

namespace eQuantic.UI.Server;

/// <summary>
/// An allowed origin that is not an origin stops the app at start, naming the value (#678): a list
/// that silently matched nothing would refuse the very page it was written for.
/// </summary>
internal sealed class ServerActionsOptionsValidator : IValidateOptions<ServerActionsOptions>
{
    public ValidateOptionsResult Validate(string? name, ServerActionsOptions options)
    {
        var malformed = options.AllowedOrigins.Where(origin => ServerActionOrigins.Normalize(origin) is null).ToList();
        return malformed.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(malformed.Select(origin =>
                $"'{origin}' in {ServerActionsOptions.SectionName}:AllowedOrigins is not an origin: write scheme://host[:port], with no path or query."));
    }
}
