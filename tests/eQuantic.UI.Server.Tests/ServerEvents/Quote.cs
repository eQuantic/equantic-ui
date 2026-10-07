namespace eQuantic.UI.Server.Tests.ServerEvents;

/// <summary>A payload with the members the wire writes as text: a decimal and a long.</summary>
public sealed record Quote(string Symbol, decimal Price, long Volume);
