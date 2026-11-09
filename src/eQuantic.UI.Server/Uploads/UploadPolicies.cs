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
}
