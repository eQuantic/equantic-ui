using Microsoft.Extensions.Options;

namespace eQuantic.UI.Server;

/// <summary>
/// Refuses, when the app starts, limits the uploads cannot run with: a limit of no unfinished upload
/// refuses every upload, and an expiry of zero removes each one as it starts.
/// </summary>
internal sealed class UploadsOptionsValidator : IValidateOptions<UploadsOptions>
{
    /// <summary>
    /// The longest a browser keeps a cookie (400 days), which is how long the cookie that names a
    /// visitor's uploads lasts: an upload kept longer would outlive the only thing that says whose it is.
    /// </summary>
    internal static readonly TimeSpan LongestExpiry = TimeSpan.FromDays(400);

    public ValidateOptionsResult Validate(string? name, UploadsOptions options)
    {
        var failures = new List<string>();
        if (options.MaxUnfinished < 1)
            failures.Add(Failure(nameof(UploadsOptions.MaxUnfinished), options.MaxUnfinished, "at least 1"));
        if (options.Expiry <= TimeSpan.Zero || options.Expiry > LongestExpiry)
            failures.Add(Failure(nameof(UploadsOptions.Expiry), options.Expiry,
                $"longer than zero and no longer than {LongestExpiry.TotalDays:0} days"));
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static string Failure(string setting, object value, string expected) =>
        $"{UploadsOptions.SectionName}:{setting} is {value}, and it must be {expected}.";
}
