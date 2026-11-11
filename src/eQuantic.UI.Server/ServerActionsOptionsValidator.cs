using Microsoft.Extensions.Options;

namespace eQuantic.UI.Server;

/// <summary>
/// An allowed origin that is not an origin stops the app at start, naming the value (#678): a list
/// that silently matched nothing would refuse the very page it was written for. So does a body cap that
/// is not a positive number of bytes, which would refuse every action.
/// </summary>
internal sealed class ServerActionsOptionsValidator : IValidateOptions<ServerActionsOptions>
{
    public ValidateOptionsResult Validate(string? name, ServerActionsOptions options)
    {
        var failures = options.AllowedOrigins.Where(origin => ServerActionOrigins.Normalize(origin) is null)
            .Select(origin =>
                $"'{origin}' in {ServerActionsOptions.SectionName}:AllowedOrigins is not an origin: write scheme://host[:port], with no path or query.")
            .ToList();
        if (options.MaxRequestBodySize <= 0)
            failures.Add($"{ServerActionsOptions.SectionName}:MaxRequestBodySize is {options.MaxRequestBodySize}: write a positive number of bytes.");
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
