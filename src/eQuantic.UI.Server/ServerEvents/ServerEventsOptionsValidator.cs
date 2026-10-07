using Microsoft.Extensions.Options;

namespace eQuantic.UI.Server;

/// <summary>
/// Refuses, when the app starts, limits the pages' connections cannot run with. Each one started
/// cleanly and failed later, one page at a time: a heartbeat interval of zero sent a heartbeat on
/// every turn of an idle stream's loop, a negative one threw on every stream, a topic limit of zero
/// refused every subscription and a payload limit of zero made every publish throw.
/// </summary>
internal sealed class ServerEventsOptionsValidator : IValidateOptions<ServerEventsOptions>
{
    /// <summary>The longest wait a cancellation timer takes, which is how the heartbeat waits.</summary>
    private static readonly TimeSpan LongestHeartbeat = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

    public ValidateOptionsResult Validate(string? name, ServerEventsOptions options)
    {
        var failures = new List<string>();
        if (options.HeartbeatInterval <= TimeSpan.Zero || options.HeartbeatInterval > LongestHeartbeat)
            failures.Add(Failure(nameof(ServerEventsOptions.HeartbeatInterval), options.HeartbeatInterval,
                $"longer than zero and no longer than {LongestHeartbeat}"));
        if (options.MaxTopicsPerConnection < 1)
            failures.Add(Failure(nameof(ServerEventsOptions.MaxTopicsPerConnection), options.MaxTopicsPerConnection, "at least 1"));
        if (options.MaxPayloadBytes < 1)
            failures.Add(Failure(nameof(ServerEventsOptions.MaxPayloadBytes), options.MaxPayloadBytes, "at least 1"));
        if (options.MaxQueuedEventsPerConnection < 1)
            failures.Add(Failure(nameof(ServerEventsOptions.MaxQueuedEventsPerConnection), options.MaxQueuedEventsPerConnection,
                "at least 1"));
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static string Failure(string setting, object value, string expected) =>
        $"{ServerEventsOptions.SectionName}:{setting} is {value}, and it must be {expected}.";
}
