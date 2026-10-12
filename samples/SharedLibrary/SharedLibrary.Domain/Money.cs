namespace SharedLibrary.Domain;

/// <summary>An amount in a currency. A page reaches it as a member of the receipt the shared
/// library declares, so it crosses to the browser with it.</summary>
public sealed record Money(decimal Amount, string Currency)
{
    /// <summary>The amount with two decimals and its currency, as the page's culture writes a number.</summary>
    public string Display => $"{Amount:0.00} {Currency}";

    public Money Plus(Money other) => this with { Amount = Amount + other.Amount };
}
