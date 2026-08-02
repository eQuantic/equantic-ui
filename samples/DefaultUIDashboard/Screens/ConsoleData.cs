using eQuantic.UI.Primitives;

namespace eQuantic.Console;

/// <summary>One settled or attempted payment — the row shape the table renders.</summary>
public sealed record Payment(
    string Id,
    string Customer,
    string Initials,
    PaymentMethod Method,
    string Date,
    PaymentStatus Status,
    string Amount);

public enum PaymentMethod
{
    Pix = 0,
    Card = 1,
    Boleto = 2,
}

public enum PaymentStatus
{
    Settled = 0,
    Pending = 1,
    Failed = 2,
}

/// <summary>A day on the volume chart: two stacked figures, in the same unit.</summary>
public sealed record VolumeDay(string Label, float Card, float Pix);

/// <summary>One line of the activity feed.</summary>
public sealed record ActivityEntry(Icons Icon, Variant Tone, string Text, string Detail, string When);

/// <summary>How far back the whole page reads — one control, several readouts.</summary>
public enum DateRange
{
    Week = 0,
    Month = 1,
    Quarter = 2,
}

/// <summary>
/// The console's sample data. A real deployment fetches this through <c>[ServerAction]</c>; keeping
/// it here keeps the sample about the UI layer, and every figure still flows through the same
/// components a live one would use.
/// </summary>
public static class ConsoleData
{
    public static readonly Payment[] Payments =
    [
        new("#3841", "Marcos Ribeiro", "MR", PaymentMethod.Pix, "17 Jul · 09:12",
            PaymentStatus.Settled, "R$ 640,00"),
        new("#3838", "Loja Verde ME", "LV", PaymentMethod.Card, "16 Jul · 18:03",
            PaymentStatus.Pending, "R$ 1.840,00"),
        new("#3835", "Julia Lemos", "JL", PaymentMethod.Boleto, "16 Jul · 11:47",
            PaymentStatus.Failed, "R$ 320,00"),
        new("#3830", "Paulo Cardoso", "PC", PaymentMethod.Pix, "15 Jul · 20:20",
            PaymentStatus.Settled, "R$ 96,90"),
        new("#3827", "Ateliê Norte", "AN", PaymentMethod.Card, "15 Jul · 08:55",
            PaymentStatus.Settled, "R$ 2.115,00"),
    ];

    public static readonly VolumeDay[] Volume =
    [
        new("11", 0.34f, 0.32f),
        new("12", 0.46f, 0.20f),
        new("13", 0.38f, 0.28f),
        new("14", 0.52f, 0.36f),
        new("15", 0.62f, 0.30f),
        new("16", 0.44f, 0.22f),
        new("17", 0.40f, 0.26f),
    ];

    public static readonly ActivityEntry[] Activity =
    [
        new(Icons.Check, Variant.Success, "Julia Lemos approved payment", "#3841", "4 min ago"),
        new(Icons.Warning, Variant.Warning, "Risk engine flagged", "#3838 for review", "22 min ago"),
        new(Icons.Info, Variant.Secondary, "Webhook retried twice", "payment.settled", "1 h ago"),
        new(Icons.Error, Variant.Destructive, "Dispute opened by", "Loja Verde", "3 h ago"),
    ];

    public static string VolumeOf(DateRange range) => range switch
    {
        DateRange.Week => "R$ 412,8k",
        DateRange.Month => "R$ 1,84M",
        _ => "R$ 5,26M",
    };

    public static string LabelOf(DateRange range) => range switch
    {
        DateRange.Week => "last 7 days",
        DateRange.Month => "last 30 days",
        _ => "last 90 days",
    };

    public static string MethodLabel(PaymentMethod method) => method switch
    {
        PaymentMethod.Pix => "PIX",
        PaymentMethod.Card => "Card ··· 4821",
        _ => "Boleto",
    };

    public static Icons MethodIcon(PaymentMethod method) => method switch
    {
        PaymentMethod.Pix => Icons.CheckCircle,
        PaymentMethod.Card => Icons.Mail,
        _ => Icons.Info,
    };

    public static Variant StatusTone(PaymentStatus status) => status switch
    {
        PaymentStatus.Settled => Variant.Success,
        PaymentStatus.Pending => Variant.Warning,
        _ => Variant.Destructive,
    };

    public static string StatusLabel(PaymentStatus status) => status switch
    {
        PaymentStatus.Settled => "Settled",
        PaymentStatus.Pending => "Pending",
        _ => "Failed",
    };
}
