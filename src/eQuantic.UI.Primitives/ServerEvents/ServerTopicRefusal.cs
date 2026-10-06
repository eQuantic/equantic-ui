namespace eQuantic.UI.Primitives;

/// <summary>A subscription the server did not bind: nothing published to the topic reaches it.</summary>
/// <param name="Topic">The topic's name.</param>
/// <param name="Reason">Why.</param>
[ZeroConstructs("primitive-values.ts: `constructor(topic = null, reason = 'forbidden')`.")]
public readonly record struct ServerTopicRefusal(string Topic, ServerTopicRefusalReason Reason);
