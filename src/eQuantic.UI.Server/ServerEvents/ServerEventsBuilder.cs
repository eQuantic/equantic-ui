using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace eQuantic.UI.Server;

/// <summary>
/// The server's half of server events, configured in <c>AddUI(ui =&gt; ui.UseServerEvents(events =&gt; …))</c>:
/// who may subscribe to which topics, and the seams an app may fill with its own piece.
/// </summary>
public sealed class ServerEventsBuilder
{
    private readonly List<ServerTopicTemplate> _templates = [];
    private readonly List<Type> _handlers = [];
    private readonly List<Action<ServerEventsOptions>> _configurations = [];
    private Type? _backplane;

    internal ServerEventsBuilder()
    {
    }

    /// <summary>
    /// Who may subscribe to the topics <paramref name="template"/> matches. The template is in
    /// ASP.NET Core's route syntax (<c>room:{roomId}</c>), its values reach the rule, and of two
    /// templates that match one topic the one whose literals fix more of it rules. A topic no template
    /// matches is refused.
    /// </summary>
    /// <exception cref="ArgumentException">The template does not parse, constrains a parameter, or its
    /// rule says neither anonymous access nor a policy nor a delegate.</exception>
    public ServerEventsBuilder Topic(string template, Action<ServerTopicRule> rule)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        ArgumentNullException.ThrowIfNull(rule);
        var configured = new ServerTopicRule(template);
        rule(configured);
        if (!configured.DecidesSomething)
            throw new ArgumentException(
                $"The rule for the topic template '{template}' says no one may subscribe and no one may not: "
                + "call AllowAnonymous(), RequireAuthorization(…) or Authorize(…).", nameof(rule));
        if (configured.IsAnonymous && (configured.RequiresAuthenticatedUser || configured.Policies.Count > 0 || configured.Delegates.Count > 0))
            throw new ArgumentException(
                $"The rule for the topic template '{template}' allows anyone and also requires something: choose one.", nameof(rule));
        _templates.Add(new ServerTopicTemplate(configured));
        return this;
    }

    /// <summary>Carries events between the app's instances through <typeparamref name="TBackplane"/>,
    /// registered as a singleton, instead of keeping them in the process.</summary>
    public ServerEventsBuilder UseBackplane<TBackplane>() where TBackplane : class, IServerEventBackplane
    {
        _backplane = typeof(TBackplane);
        return this;
    }

    /// <summary>Lets <typeparamref name="THandler"/> hear connections open and close and topics bound and
    /// released, resolved from each request's services.</summary>
    public ServerEventsBuilder AddHandler<THandler>() where THandler : class, IServerEventHandler
    {
        _handlers.Add(typeof(THandler));
        return this;
    }

    /// <summary>Overrides the limits read from <c>EQuantic:ServerEvents</c>.</summary>
    public ServerEventsBuilder Configure(Action<ServerEventsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _configurations.Add(configure);
        return this;
    }

    internal void Register(IServiceCollection services)
    {
        var options = services.AddOptions<ServerEventsOptions>().BindConfiguration(ServerEventsOptions.SectionName);
        foreach (var configure in _configurations) options.Configure(configure);
        // A limit the connections cannot run with stops the app from starting, instead of failing
        // each page that connects.
        options.ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<ServerEventsOptions>, ServerEventsOptionsValidator>());

        if (_backplane is null) services.TryAddSingleton<IServerEventBackplane, InMemoryServerEventBackplane>();
        else services.Replace(ServiceDescriptor.Singleton(typeof(IServerEventBackplane), _backplane));

        services.TryAddSingleton<ServerEventConnections>();
        services.TryAddSingleton<IServerEventPublisher, ServerEventPublisher>();
        services.AddSingleton(new ServerTopicAuthorizer([.. _templates]));
        services.AddHostedService<ServerEventsBackplaneListener>();
        foreach (var handler in _handlers) services.AddScoped(typeof(IServerEventHandler), handler);
    }
}
