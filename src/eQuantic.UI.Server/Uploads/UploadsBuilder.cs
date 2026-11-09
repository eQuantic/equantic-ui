using eQuantic.UI.Primitives;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace eQuantic.UI.Server;

/// <summary>
/// The server's half of uploads, configured in <c>AddUI(ui =&gt; ui.UseUploads(uploads =&gt; …))</c>:
/// the policies it accepts files under, and the limits read from <c>EQuantic:Uploads</c>.
/// </summary>
public sealed class UploadsBuilder
{
    private readonly Dictionary<string, UploadPolicy> _policies = new(StringComparer.Ordinal);
    private readonly List<Action<UploadsOptions>> _configurations = [];

    internal UploadsBuilder()
    {
    }

    /// <summary>
    /// Accepts files under <paramref name="policy"/>: an upload that names it is refused when its file
    /// is not one of the policy's formats or is larger than its largest size. Several policies may be
    /// accepted, each under its own name; the same policy accepted twice, by a library and by the app,
    /// is accepted once.
    /// </summary>
    /// <exception cref="ArgumentException">Another policy is accepted under the same name.</exception>
    public UploadsBuilder Accept(UploadPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (_policies.TryGetValue(policy.Name, out var accepted))
        {
            if (ReferenceEquals(accepted, policy)) return this;
            throw new ArgumentException(
                $"Another upload policy is already accepted under the name '{policy.Name}': each policy's name must be unique.",
                nameof(policy));
        }

        _policies.Add(policy.Name, policy);
        return this;
    }

    /// <summary>Overrides the settings read from <c>EQuantic:Uploads</c>.</summary>
    public UploadsBuilder Configure(Action<UploadsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _configurations.Add(configure);
        return this;
    }

    internal void Register(IServiceCollection services)
    {
        var options = services.AddOptions<UploadsOptions>().BindConfiguration(UploadsOptions.SectionName);
        foreach (var configure in _configurations) options.Configure(configure);
        // A limit the uploads cannot run with stops the app from starting, instead of refusing each
        // person who uploads.
        options.ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<UploadsOptions>, UploadsOptionsValidator>());

        services.TryAddSingleton(TimeProvider.System);
        // Read when the first upload arrives, after every Accept the app's AddUI made.
        services.TryAddSingleton(_ => new UploadPolicies(_policies.Values));
        services.TryAddSingleton<UploadStore>();
        services.TryAddSingleton<UploadProtocol>();
        services.AddHostedService<ExpiredUploadsCleaner>();
    }
}
