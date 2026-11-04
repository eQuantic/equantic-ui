namespace eQuantic.UI.Primitives;

/// <summary>
/// A kind of file the app takes from a person, declared once in C# that the page and the server both
/// read: its name, the formats it accepts and the largest file it takes. The server refuses what the
/// policy does not accept, whatever the page's own picker let through, so the page's filter is a
/// courtesy to the person and this is the rule.
/// <code>
/// public static readonly UploadPolicy Models = new("models",
///     filters: [new FileFilter("3D models", "stl", "obj", "3mf")],
///     maxSize: 250 * 1024 * 1024);
///
/// builder.Services.AddUI(ui =&gt; ui.UseUploads(uploads =&gt; uploads.Accept(Models)));
/// </code>
/// <para>
/// It is a declaration rather than a value, so two policies are the same policy when they are the same
/// object; the server knows a policy by its <see cref="Name"/>, which is unique among the ones it
/// accepts.
/// </para>
/// </summary>
public sealed class UploadPolicy
{
    /// <summary>A kind of upload.</summary>
    /// <param name="name">The name the server knows the policy by, unique among the policies it
    /// accepts.</param>
    /// <param name="filters">The formats it accepts, extensions without the dot as
    /// <see cref="FileFilter"/> states them. Empty means any file.</param>
    /// <param name="maxSize">The largest file it accepts, in bytes.</param>
    /// <param name="requireSignedIn">Whether only a signed-in person may upload under it. A visitor's
    /// upload is otherwise kept for the browser that sent it.</param>
    /// <exception cref="ArgumentException">The name is empty or white space.</exception>
    /// <exception cref="ArgumentNullException">The name or the filters are null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The largest size is not positive.</exception>
    public UploadPolicy(string name, IReadOnlyList<FileFilter> filters, long maxSize, bool requireSignedIn = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(filters);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSize);
        Name = name;
        Filters = filters;
        MaxSize = maxSize;
        RequireSignedIn = requireSignedIn;
    }

    /// <summary>The name the server knows the policy by.</summary>
    public string Name { get; }

    /// <summary>The formats the policy accepts; empty means any file.</summary>
    public IReadOnlyList<FileFilter> Filters { get; }

    /// <summary>The largest file the policy accepts, in bytes.</summary>
    public long MaxSize { get; }

    /// <summary>Whether only a signed-in person may upload under the policy.</summary>
    public bool RequireSignedIn { get; }
}
