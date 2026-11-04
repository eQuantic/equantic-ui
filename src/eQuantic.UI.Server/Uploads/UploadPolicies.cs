using eQuantic.UI.Primitives;

namespace eQuantic.UI.Server;

/// <summary>The policies the app accepts uploads under, by name, and what each one accepts.</summary>
internal sealed class UploadPolicies
{
    private readonly Dictionary<string, UploadPolicy> _byName;

    public UploadPolicies(IEnumerable<UploadPolicy> policies)
    {
        _byName = policies.ToDictionary(policy => policy.Name, StringComparer.Ordinal);
        LargestSize = _byName.Count == 0 ? 0 : _byName.Values.Max(policy => policy.MaxSize);
    }

    /// <summary>The largest file any policy accepts, in bytes; zero when the app accepts none.</summary>
    public long LargestSize { get; }

    /// <summary>The policy named <paramref name="name"/>, or null when the app accepts none of that name.</summary>
    public UploadPolicy? Find(string? name) => name is not null && _byName.TryGetValue(name, out var policy) ? policy : null;

    /// <summary>
    /// Whether <paramref name="policy"/> accepts a file named <paramref name="fileName"/>: any file when
    /// it has no filters, otherwise one whose name ends in one of its extensions, compared without
    /// regard to case. The END of the name is compared, not the text after its last dot, so an
    /// extension of two parts (<c>tar.gz</c>) matches as written. A leading dot in a filter is read
    /// as absent, as <see cref="FileFilter"/> states extensions without it.
    /// </summary>
    public static bool Accepts(UploadPolicy policy, string fileName) =>
        policy.Filters.Count == 0
        || policy.Filters.SelectMany(filter => filter.Extensions)
            .Select(extension => extension.TrimStart('.'))
            .Any(extension => extension.Length > 0
                              && fileName.EndsWith("." + extension, StringComparison.OrdinalIgnoreCase));

    /// <summary>The extensions <paramref name="policy"/> accepts, as a reason names them: ".stl, .obj".</summary>
    public static string Formats(UploadPolicy policy) =>
        string.Join(", ", policy.Filters.SelectMany(filter => filter.Extensions)
            .Select(extension => "." + extension.TrimStart('.'))
            .Distinct(StringComparer.OrdinalIgnoreCase));
}
