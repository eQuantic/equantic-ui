namespace eQuantic.Wallet;

/// <summary>
/// Where the app's money comes from. A real one would talk to a bank; this one reads the fixture
/// beside it. What matters is the SHAPE: the screen asks an interface for its data and is handed an
/// implementation by the container, exactly as any other .NET class would be.
/// </summary>
public interface IWalletLedger
{
    string HolderName { get; }
    decimal Balance { get; }
    IReadOnlyList<Entry> Recent { get; }
    IReadOnlyList<Entry> Today { get; }
    IReadOnlyList<Entry> Yesterday { get; }
}

public sealed class WalletLedger : IWalletLedger
{
    public string HolderName => "Ana Beatriz";
    public decimal Balance => 12_480m;
    public IReadOnlyList<Entry> Recent => WalletData.Recent;
    public IReadOnlyList<Entry> Today => WalletData.Today;
    public IReadOnlyList<Entry> Yesterday => WalletData.Yesterday;
}
