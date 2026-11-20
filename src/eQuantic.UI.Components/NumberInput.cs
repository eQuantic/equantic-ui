using System.Globalization;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// A whole number, typed, with its unit beside it: a lead time in days, a duration in minutes, a weight in
/// grams. A <see cref="TextInput"/> of kind <see cref="TextEntryKind.Number"/>, so a phone opens the digits,
/// and the unit is its suffix, part of the field's accessible name.
/// <para>
/// Where <see cref="Stepper"/> steps a small count with two buttons, this takes a number typed whole: 250 g
/// is one word here and fifty presses there. The number is written as the culture writes it, grouped while
/// the field rests and plain while it is edited, read in the culture with no group separator, and handed on as soon as it is one
/// inside <see cref="Min"/> and <see cref="Max"/>; null when the field is emptied, and nothing when what is
/// typed is no whole number or is out of range, which the field says.
/// </para>
/// </summary>
public sealed class NumberInput : StatefulComponent
{
    /// <summary>What the reader has typed, while it has not been settled; null shows the value.</summary>
    private string? _typing;

    private bool _focused;

    public NumberInput(int? value, Action<int?>? onChanged, string label, string unit = "",
        int? min = null, int? max = null, string? placeholder = null, string? helper = null,
        string? error = null, SizeVariant size = SizeVariant.Large)
    {
        Value = value;
        OnChanged = onChanged;
        Label = label;
        Unit = unit;
        Min = min;
        Max = max;
        Placeholder = placeholder;
        Helper = helper;
        Error = error;
        Size = size;
    }

    public int? Value { get; private set; }

    /// <summary>The number the reader typed, or null when they emptied the field.</summary>
    public Action<int?>? OnChanged { get; private set; }

    public string Label { get; private set; }

    /// <summary>What the number counts, written after it and announced with the field's name: <c>days</c>,
    /// <c>min</c>, <c>g</c>. The app's own word, in its own language.</summary>
    public string Unit { get; private set; }

    public int? Min { get; private set; }
    public int? Max { get; private set; }
    public string? Placeholder { get; private set; }
    public string? Helper { get; private set; }

    /// <summary>The app's own error, shown instead of the field's.</summary>
    public string? Error { get; private set; }

    public SizeVariant Size { get; private set; }

    /// <summary>Backed by a field, so <see cref="AdoptConfig"/> can carry it.</summary>
    public bool Disabled { get => _disabled; init => _disabled = value; }
    private bool _disabled;

    public override void AdoptConfig(UiComponent next)
    {
        if (next is not NumberInput fresh) return;

        // As MoneyInput: the buffer outlives a rebuild that hands back what it said, or that left the value
        // where it was, and goes when the app moved the value somewhere it does not say.
        if (_typing is { } buffer && fresh.Value != Value && !Says(buffer, fresh.Value)) _typing = null;

        Value = fresh.Value;
        OnChanged = fresh.OnChanged;
        Label = fresh.Label;
        Unit = fresh.Unit;
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
        var shown = _typing ?? (Value is { } value ? Write(value, _focused) : "");
        // The app's error first: it knows what the field is for.
        var error = Error ?? (_typing is { } typed ? Complaint(typed) : null);
        return new TextInput(shown, OnChanged is null ? null : Type, Label, Placeholder, Helper, error,
            size: Size, kind: TextEntryKind.Number, suffix: Unit.Length > 0 ? Unit : null)
        {
            Disabled = Disabled,
            OnFocusChanged = Focus,
        };
    }

    private void Type(string text)
    {
        if (text.Trim().Length == 0) OnChanged?.Invoke(null);
        else if (Read(text) is { } number && InRange(number)) OnChanged?.Invoke(number);
        SetState(() => _typing = text);
    }

    private void Focus(bool focused) => SetState(() =>
    {
        _focused = focused;
        if (!focused && _typing is { } typed && Complaint(typed) is null) _typing = null;
    });

    /// <summary>What the field says is wrong with what was typed, or null.</summary>
    private string? Complaint(string typed)
    {
        if (typed.Trim().Length == 0) return null;
        if (Read(typed) is not { } number) return SdkStrings.WholeNumberExpected(Write(12, true));
        if (InRange(number)) return null;
        if (Min is { } min && Max is { } max) return SdkStrings.ValueBetween(WithUnit(min), WithUnit(max));
        return Min is { } least ? SdkStrings.ValueAtLeast(WithUnit(least)) : SdkStrings.ValueAtMost(WithUnit(Max!.Value));
    }

    private bool InRange(int number) => (Min is not { } min || number >= min) && (Max is not { } max || number <= max);

    /// <summary>The number as the field writes it: grouped at rest, plain while edited.</summary>
    private static string Write(int number, bool plain) => number.ToString(plain ? "D" : "N0");

    /// <summary>A bound as a message names it: the number, then the unit.</summary>
    private string WithUnit(int number) => Unit.Length > 0 ? $"{number:N0} {Unit}" : $"{number:N0}";

    private static bool Says(string typed, int? number) =>
        typed.Trim().Length == 0 ? number is null : Read(typed) is { } read && read == number;

    /// <summary>
    /// What was typed as a whole number in the culture, or null. No group separators: the field shows the
    /// number plain while it is edited, so none is ever typed around, and .NET reads them wherever they
    /// stand, which made English's <c>2,5</c> twenty-five.
    /// </summary>
    private static int? Read(string typed) =>
        int.TryParse(typed.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var number) ? number : null;
}
