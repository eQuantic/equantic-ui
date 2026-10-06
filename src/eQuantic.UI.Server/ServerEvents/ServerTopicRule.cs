namespace eQuantic.UI.Server;

/// <summary>
/// Who may subscribe to the topics one template matches: anyone, the users an ASP.NET Core
/// authorization policy accepts, or the requests a delegate accepts. A policy and a delegate may be
/// combined, and then both must accept. A rule that says nothing refuses to be configured.
/// </summary>
public sealed class ServerTopicRule
{
    private readonly List<string> _policies = [];
    private readonly List<Func<ServerTopicContext, ValueTask<bool>>> _delegates = [];

    internal ServerTopicRule(string template) => Template = template;

    /// <summary>The template, in ASP.NET Core's route syntax.</summary>
    public string Template { get; }

    internal bool IsAnonymous { get; private set; }

    internal bool RequiresAuthenticatedUser { get; private set; }

    internal IReadOnlyList<string> Policies => _policies;

    internal IReadOnlyList<Func<ServerTopicContext, ValueTask<bool>>> Delegates => _delegates;

    internal bool DecidesSomething => IsAnonymous || RequiresAuthenticatedUser || _policies.Count > 0 || _delegates.Count > 0;

    /// <summary>Anyone may subscribe, signed in or not.</summary>
    public ServerTopicRule AllowAnonymous()
    {
        IsAnonymous = true;
        return this;
    }

    /// <summary>
    /// The user must satisfy every named policy, evaluated by ASP.NET Core's
    /// <c>IAuthorizationService</c> with the topic (<see cref="ServerTopicContext"/>) as the resource,
    /// so a policy's handlers read the template's values. With no name, the user must be signed in.
    /// </summary>
    public ServerTopicRule RequireAuthorization(params string[] policies)
    {
        if (policies.Length == 0) RequiresAuthenticatedUser = true;
        foreach (var policy in policies)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(policy, nameof(policies));
            _policies.Add(policy);
        }
        return this;
    }

    /// <summary>The delegate must accept the request: it reads the template's values and the
    /// <see cref="ServerTopicContext.HttpContext"/>.</summary>
    public ServerTopicRule Authorize(Func<ServerTopicContext, ValueTask<bool>> authorize)
    {
        ArgumentNullException.ThrowIfNull(authorize);
        _delegates.Add(authorize);
        return this;
    }

    /// <summary>The delegate must accept the request: it reads the template's values and the
    /// <see cref="ServerTopicContext.HttpContext"/>.</summary>
    public ServerTopicRule Authorize(Func<ServerTopicContext, bool> authorize)
    {
        ArgumentNullException.ThrowIfNull(authorize);
        _delegates.Add(context => ValueTask.FromResult(authorize(context)));
        return this;
    }
}
