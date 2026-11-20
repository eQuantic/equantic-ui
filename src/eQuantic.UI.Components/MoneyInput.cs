using System.Globalization;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// An amount of money, typed: a <see cref="TextInput"/> of kind <see cref="TextEntryKind.Decimal"/>, so a
/// phone opens the decimal keypad, holding an amount in the currency's minor units (cents for the euro).
/// <para>
/// The field writes the amount as the culture writes a number with the currency's digits (<c>1 234,50</c>),
/// and what is typed stays as it was typed until the field is left: taking the focus rewrites nothing, since a
/// value written under a pointer's press moves the caret to the end. The currency's symbol is not part of the text, which the reader would have to type around:
/// it is the field's prefix or suffix, on the side the culture writes its own (<c>12,50 €</c> in pt-PT,
/// <c>€ 12.50</c> in English), and part of the field's accessible name.
/// </para>
/// <para>
/// What is typed is read in the culture with a decimal point and no group separator; then with the other of
/// <c>.</c> and <c>,</c> as the point, because a phone's decimal keypad shows the DEVICE's separator, which
/// need not be the page's; then with the culture's group separators, for a pasted <c>1 234,50</c>. A reading
/// with more fraction digits than the currency has is not one, so English reads <c>12,50</c> as twelve and a
/// half and <c>1,234</c> as a thousand and more. The amount is handed on as soon as what is typed is one
/// inside <see cref="Min"/> and <see cref="Max"/>, as <see cref="DatePicker"/> hands on a date, and null when
/// the field is emptied; anything else is not handed on, and the field says what it wants.
/// </para>
/// </summary>
public sealed class MoneyInput : StatefulComponent
{
    /// <summary>What the reader has TYPED, while it has not been settled. Null means the field shows the
    /// amount instead: a half-typed amount is not an amount, and reformatting under the caret is the thing
    /// every money field gets wrong.</summary>
    private string? _typing;

    public MoneyInput(long? amount, Action<long?>? onChanged, string label, Currency currency,
        long? min = null, long? max = null, string? placeholder = null, string? helper = null,
        string? error = null, SizeVariant size = SizeVariant.Large)
    {
        // A field of text, which has no small size (spec B9): refused here, where TextInput would refuse it at
        // the first build, inside the page's error boundary.
        if (size == SizeVariant.Small)
            throw new ArgumentOutOfRangeException(nameof(size),
                "A text field has no Small size: text and padding cannot fit 32dp (spec B9).");
        Amount = amount;
        OnChanged = onChanged;
        Label = label;
        Currency = currency;
        Min = min;
        Max = max;
        Placeholder = placeholder;
        Helper = helper;
        Error = error;
        Size = size;
    }

    /// <summary>The amount in the currency's minor units, or null for none.</summary>
    public long? Amount { get; private set; }

    /// <summary>The amount the reader typed, in minor units, or null when they emptied the field.</summary>
    public Action<long?>? OnChanged { get; private set; }

    public string Label { get; private set; }
    public Currency Currency { get; private set; }

    /// <summary>The least amount the field hands on, in minor units.</summary>
    public long? Min { get; private set; }

    /// <summary>The greatest amount the field hands on, in minor units.</summary>
    public long? Max { get; private set; }

    public string? Placeholder { get; private set; }
    public string? Helper { get; private set; }

    /// <summary>The app's own error (a required amount left empty), shown instead of the field's.</summary>
    public string? Error { get; private set; }

    public SizeVariant Size { get; private set; }

    /// <summary>Backed by a field, so <see cref="AdoptConfig"/> can carry it: this instance is kept across the
    /// parent's rebuilds, and an <c>init</c> accessor cannot be written from there.</summary>
    public bool Disabled { get => _disabled; init => _disabled = value; }
    private bool _disabled;

    public override void AdoptConfig(UiComponent next)
    {
        if (next is not MoneyInput fresh) return;

        // What the reader typed outlives a re-render, or a parent that rebuilds on every keystroke would
        // erase the amount being written. It must not outlive the app moving the amount elsewhere (a form
        // reset), and the test is whether the app MOVED it and the buffer does not say where it went: an
        // amount the field handed on comes straight back, and one that was never handed on (a half-typed
        // or out-of-range one) leaves the amount where it was.
        if (_typing is { } buffer && fresh.Amount != Amount && !Says(buffer, fresh.Amount, fresh.Currency)) _typing = null;

        Amount = fresh.Amount;
        OnChanged = fresh.OnChanged;
        Label = fresh.Label;
        Currency = fresh.Currency;
        Min = fresh.Min;
        Max = fresh.Max;
        Placeholder = fresh.Placeholder;
        Helper = fresh.Helper;
        Error = fresh.Error;
        Size = fresh.Size;
        _disabled = fresh.Disabled;
    }

    public override VisualNode Build(ComponentContext context)
    {
        var shown = _typing ?? (Amount is { } amount ? Write(amount) : "");
        // The symbol goes where the culture writes its own: before the amount for $n and $ n, after it for
        // n$ and n $.
        var leads = NumberFormatInfo.CurrentInfo.CurrencyPositivePattern is 0 or 2;
        // The app's error first: it knows what the field is for.
        var error = Error ?? (_typing is { } typed ? Complaint(typed) : null);
        return new TextInput(shown, OnChanged is null ? null : Type, Label, Placeholder, Helper, error,
            size: Size, kind: TextEntryKind.Decimal,
            prefix: leads ? Currency.Symbol : null, suffix: leads ? null : Currency.Symbol)
        {
            Disabled = Disabled,
            OnFocusChanged = Focus,
        };
    }

    /// <summary>A keystroke: the amount is handed on the moment what is typed is one the field takes.</summary>
    private void Type(string text)
    {
        if (text.Trim().Length == 0) OnChanged?.Invoke(null);
        else if (Read(text, Currency) is { } amount && InRange(amount)) OnChanged?.Invoke(amount);
        SetState(() => _typing = text);
    }

    /// <summary>Leaving settles what was typed into the amount's own text, unless it is not one the field
    /// takes, which stays for the reader to correct.</summary>
    private void Focus(bool focused)
    {
        if (!focused && _typing is { } typed && Complaint(typed) is null) SetState(() => _typing = null);
    }

    /// <summary>What the field says is wrong with what was typed, or null.</summary>
    private string? Complaint(string typed)
    {
        if (typed.Trim().Length == 0) return null;
        if (Read(typed, Currency) is not { } amount) return SdkStrings.AmountExpected(Write(1250));
        if (InRange(amount)) return null;
        if (Min is { } min && Max is { } max) return SdkStrings.ValueBetween(Currency.Format(min), Currency.Format(max));
        return Min is { } least ? SdkStrings.ValueAtLeast(Currency.Format(least)) : SdkStrings.ValueAtMost(Currency.Format(Max!.Value));
    }

    private bool InRange(long amount) => (Min is not { } min || amount >= min) && (Max is not { } max || amount <= max);

    /// <summary>The amount as the field writes it: the culture's number, grouped, with the currency's digits.</summary>
    private string Write(long amount) => Currency.ToMajor(amount).ToString("N" + Currency.MinorDigits);

    /// <summary>Whether what was typed says <paramref name="amount"/>: nothing for none, or that amount. Text
    /// that is no amount says nothing at all, not even none.</summary>
    private static bool Says(string typed, long? amount, Currency currency) =>
        typed.Trim().Length == 0 ? amount is null : Read(typed, currency) is { } read && read == amount;

    /// <summary>
    /// What was typed, in minor units, or null when it is no amount of the currency: read in the culture
    /// with a decimal point, then with the other of <c>.</c> and <c>,</c> as that point, then with the
    /// culture's group separators, the first reading with no more fraction digits than the currency has.
    /// </summary>
    private static long? Read(string typed, Currency currency)
    {
        var text = typed.Trim();
        const NumberStyles Plain = NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite
            | NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        if (decimal.TryParse(text, Plain, CultureInfo.CurrentCulture, out var value) && currency.ToMinor(value) is { } read)
            return read;

        var point = NumberFormatInfo.CurrentInfo.NumberDecimalSeparator;
        var other = point == "," ? "." : point == "." ? "," : null;
        if (other is not null && decimal.TryParse(text.Replace(other, point), Plain, CultureInfo.CurrentCulture, out var swapped)
            && currency.ToMinor(swapped) is { } readSwapped)
            return readSwapped;

        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var grouped)
            ? currency.ToMinor(grouped)
            : null;
    }
}
