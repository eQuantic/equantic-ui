using eQuantic.UI.Primitives;

namespace eQuantic.Wallet;

/// <summary>The six screens of the handoff, in the order a reviewer walks them.</summary>
public enum WalletScreen
{
    Home = 0,
    Transactions = 1,
    Detail = 2,
    Settings = 3,
    Cards = 4,
    Onboarding = 5,
}

/// <summary>One line of the ledger. <see cref="Amount"/> is signed text — a wallet shows the sign.</summary>
public sealed record Entry(
    Icons Icon,
    Variant Tone,
    string Title,
    string Subtitle,
    string Amount,
    bool Incoming = false,
    string? Badge = null);

/// <summary>One of the four tabs the bottom bar carries.</summary>
public sealed record Destination(Icons Icon, string Label, int Badge = 0,
    WalletScreen Screen = WalletScreen.Home);

/// <summary>
/// The wallet's sample data. A real app fetches this through <c>[ServerAction]</c>; keeping it here
/// keeps the sample about the UI layer, and every figure still flows through the same components.
/// </summary>
public static class WalletData
{
    public const string Balance = "R$ 12.480,00";
    public const string Owner = "Ana Beatriz";

    public static readonly Destination[] Tabs =
    [
        new(Icons.CheckCircle, "Home", Screen: WalletScreen.Home),
        new(Icons.Mail, "Cards", 2, WalletScreen.Cards),
        new(Icons.ChevronRight, "Transfer", Screen: WalletScreen.Transactions),
        new(Icons.Person, "Profile", Screen: WalletScreen.Settings),
    ];

    public static readonly Entry[] Recent =
    [
        new(Icons.Check, Variant.Success, "Ana Beatriz N.", "PIX received · 09:12", "+ 1.200,00", Incoming: true),
        new(Icons.Warning, Variant.Secondary, "Enel energia", "Bill · yesterday", "− 284,30"),
        new(Icons.Mail, Variant.Secondary, "Livraria Cultura", "Card ··· 4821", "− 96,90"),
    ];

    public static readonly Entry[] Today =
    [
        new(Icons.Check, Variant.Success, "Marcos Ribeiro", "PIX received · 09:12", "+ 640,00", Incoming: true),
        new(Icons.Mail, Variant.Secondary, "Cantina do Porto", "Card ··· 4821 · 12:40", "− 78,00"),
        new(Icons.Info, Variant.Warning, "Aluguel · escritório", "Scheduled for tomorrow", "", Badge: "Pending"),
    ];

    public static readonly Entry[] Yesterday =
    [
        new(Icons.Warning, Variant.Secondary, "Enel energia", "Bill payment", "− 284,30"),
    ];

    /// <summary>The key/value rows of a transaction's detail — label left, value right.</summary>
    public static readonly (string Label, string Value, bool Mono)[] DetailRows =
    [
        ("Method", "PIX", false),
        ("Date", "17 Jul 2026 · 09:12", false),
        ("Account", "Checking ··· 4821", false),
        ("Transaction ID", "E18236·0311", true),
    ];

    public static readonly (string Initials, string Name)[] RecentPeople =
    [
        ("MR", "Marcos"),
        ("JL", "Julia"),
        ("PC", "Paulo"),
    ];

    public static string TitleOf(WalletScreen screen) => screen switch
    {
        WalletScreen.Home => "Home",
        WalletScreen.Transactions => "Transactions",
        WalletScreen.Detail => "Detail + sheet",
        WalletScreen.Settings => "Settings",
        WalletScreen.Cards => "Empty + dialog",
        _ => "Flow step",
    };
}
