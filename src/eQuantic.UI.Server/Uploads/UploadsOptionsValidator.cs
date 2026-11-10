using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace eQuantic.UI.Server;

/// <summary>
/// Refuses, when the app starts, limits the uploads cannot run with: a limit of no unfinished upload,
/// or of no upload or byte at all, refuses every upload, and an expiry of zero removes each one as it
/// starts.
/// </summary>
internal sealed class UploadsOptionsValidator(IHostEnvironment environment, IServiceProvider services) : IValidateOptions<UploadsOptions>
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
        if (options.MaxUploadsPerOwner < 1)
            failures.Add(Failure(nameof(UploadsOptions.MaxUploadsPerOwner), options.MaxUploadsPerOwner, "at least 1"));
        if (options.MaxBytesPerOwner < 1)
            failures.Add(Failure(nameof(UploadsOptions.MaxBytesPerOwner), options.MaxBytesPerOwner, "at least 1"));
        if (options.MaxTotalUploads < 1)
            failures.Add(Failure(nameof(UploadsOptions.MaxTotalUploads), options.MaxTotalUploads, "at least 1"));
        if (options.MaxTotalBytes < 1)
            failures.Add(Failure(nameof(UploadsOptions.MaxTotalBytes), options.MaxTotalBytes, "at least 1"));
        if (options.Expiry <= TimeSpan.Zero || options.Expiry > LongestExpiry)
            failures.Add(Failure(nameof(UploadsOptions.Expiry), options.Expiry,
                $"longer than zero and no longer than {LongestExpiry.TotalDays:0} days"));
        // A policy that accepts a file larger than a byte quota could never store one: every upload under it
        // would be refused at run time, so the app stops at start, naming it.
        if (services.GetService<UploadPolicies>() is { LargestSize: > 0 } policies)
        {
            foreach (var policy in policies.All.Where(policy => policy.MaxSize > Math.Min(options.MaxBytesPerOwner, options.MaxTotalBytes)))
                failures.Add($"The upload policy '{policy.Name}' accepts files of {policy.MaxSize} bytes, more than " +
                             $"{UploadsOptions.SectionName}:{(options.MaxBytesPerOwner <= options.MaxTotalBytes ? nameof(UploadsOptions.MaxBytesPerOwner) : nameof(UploadsOptions.MaxTotalBytes))} " +
                             $"allows ({Math.Min(options.MaxBytesPerOwner, options.MaxTotalBytes)}): raise it, or lower the policy's size.");
        }
        if (UnderTheWebRoot(UploadStore.FolderOf(options, environment)) is { } root)
            failures.Add($"{UploadsOptions.SectionName}:{nameof(UploadsOptions.Folder)} is under the web root {root}, " +
                         "where the static files would serve every upload and the cookie of the person who sent it: choose a folder outside it.");
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    /// <summary>The web root <paramref name="folder"/> is in, or null when it is outside it (#1293).</summary>
    private string? UnderTheWebRoot(string folder)
    {
        // The web host's environment, which a web app registers beside the generic host's: only it knows
        // the web root.
        if (services.GetService<IWebHostEnvironment>() is not { } web) return null;
        // ASP.NET Core leaves the web root unset when its folder does not exist yet, which an upload folder
        // inside it would then create: the default one is the one the static files serve from then on
        // (found in the review of #1326).
        var webRoot = web.WebRootPath is { Length: > 0 } configured ? configured : Path.Combine(environment.ContentRootPath, "wwwroot");
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(webRoot));
        var kept = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        var comparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return kept.Equals(root, comparison) || kept.StartsWith(root + Path.DirectorySeparatorChar, comparison) ? root : null;
    }

    private static string Failure(string setting, object value, string expected) =>
        $"{UploadsOptions.SectionName}:{setting} is {value}, and it must be {expected}.";
}
