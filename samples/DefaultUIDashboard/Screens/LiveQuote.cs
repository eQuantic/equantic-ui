namespace eQuantic.Console;

/// <summary>
/// What the server publishes to the live screen: a decimal and a long, the two values a JSON number
/// would round, so a payload that arrived as a number instead of being revived would show on the page.
/// </summary>
public sealed record LiveQuote(string Symbol, decimal Price, long Sequence);
