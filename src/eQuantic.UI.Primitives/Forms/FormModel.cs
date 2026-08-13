namespace eQuantic.UI.Primitives;

/// <summary>
/// One rule a field must satisfy, and what to say when it does not.
/// <para>
/// A rule is a PREDICATE, not a description of one, because this type has to run in a browser.
/// <c>System.ComponentModel.DataAnnotations</c> validates by reflection, and reflection is exactly
/// what the transpiler cannot carry — so the attributes a model already carries are read at BUILD
/// time and emitted as rules like these. What runs on either target is a lambda over a string.
/// </para>
/// <para>
/// The message is a plain string rather than a resource key, and that is a fence, not an oversight:
/// an app that localizes its messages passes localized strings in, because it is the app that owns
/// its resx. The SDK's own chrome is the only text this framework translates on your behalf.
/// </para>
/// </summary>
public sealed record FieldRule(string Message, Func<string, bool> Holds);

/// <summary>
/// The rules a form is usually made of. Each returns a <see cref="FieldRule"/>, so a hand-written
/// form and a generated one are the same shape by construction — the DataAnnotations bridge emits
/// calls to these, and there is no second validation engine to keep in step.
/// <para>
/// Every rule but <see cref="Required"/> passes on an EMPTY value. A field that is optional and
/// blank is not badly formatted, it is absent... and "Enter a valid email" under an empty optional
/// box is the single most common false alarm in form validation.
/// </para>
/// </summary>
public static class Rules
{
    public static FieldRule Required(string message = "This field is required.") =>
        new(message, value => value.Trim().Length > 0);

    public static FieldRule MinLength(int length, string? message = null) =>
        new(message ?? $"Use at least {length} characters.",
            value => value.Length == 0 || value.Length >= length);

    public static FieldRule MaxLength(int length, string? message = null) =>
        new(message ?? $"Use at most {length} characters.", value => value.Length <= length);

    /// <summary>
    /// An address that could exist: something before an @, something after it, and a dot in the
    /// domain. Deliberately not RFC 5322 — the full grammar accepts addresses no provider issues
    /// and rejecting a real address is worse than accepting a typo the confirmation email catches.
    /// </summary>
    public static FieldRule Email(string message = "Enter a valid email address.") =>
        new(message, value =>
        {
            if (value.Length == 0) return true;
            var at = value.IndexOf('@');
            if (at <= 0 || at != value.LastIndexOf('@')) return false;
            var domain = value[(at + 1)..];
            var dot = domain.IndexOf('.');
            return dot > 0 && dot < domain.Length - 1 && !domain.Contains(' ') && !value[..at].Contains(' ');
        });

    /// <summary>A number inside a closed interval. Text that is not a number fails: a range is a
    /// statement about a number, and something that is not one cannot satisfy it.</summary>
    public static FieldRule Range(double min, double max, string? message = null) =>
        new(message ?? $"Enter a number between {min} and {max}.", value =>
        {
            if (value.Length == 0) return true;
            return double.TryParse(value, System.Globalization.NumberStyles.Any,
                       System.Globalization.CultureInfo.InvariantCulture, out var number)
                   && number >= min && number <= max;
        });

    /// <summary>The value must equal another field's — a password confirmation, an email retype.
    /// It reads the OTHER field at validation time, so it is always judged against what is on
    /// screen rather than against what was there when the form was built.</summary>
    public static FieldRule Matches(FormField other, string? message = null) =>
        new(message ?? "The two values do not match.", value => value == other.Value);

    /// <summary>An arbitrary predicate, for the rule this list does not have. The escape hatch is
    /// part of the design: a closed rule set gets worked around with a fake one.</summary>
    public static FieldRule Custom(string message, Func<string, bool> holds) => new(message, holds);
}

/// <summary>
/// One field of a <see cref="FormController"/>: what the user has typed, what has happened to it,
/// and what is currently wrong with it.
/// <para>
/// The two flags are not the same question and forms that conflate them nag. TOUCHED means the
/// user has left the field, and it is the flag that decides whether an error may be SHOWN. DIRTY
/// means the value differs from the one the form opened with, and it is the flag that answers
/// "are there unsaved changes".
/// </para>
/// </summary>
public sealed class FormField
{
    private readonly List<FieldRule> _rules;

    public FormField(string name, string initial = "", IReadOnlyList<FieldRule>? rules = null)
    {
        Name = name;
        Initial = initial;
        Value = initial;
        _rules = rules is null ? [] : [.. rules];
        // A form is invalid the moment it opens if a required field is empty, and it has to KNOW
        // that before anyone types: without this the first submit found nothing wrong and ran.
        // (Saying nothing yet is `VisibleError`'s job, not this one's.)
        Revalidate();
    }

    /// <summary>The field's own name — the key a controller looks it up by, and what a generated
    /// validator names when it reports.</summary>
    public string Name { get; }

    /// <summary>What the form opened with. <see cref="Dirty"/> is measured against this, and
    /// <see cref="Reset"/> returns here.</summary>
    public string Initial { get; private set; }

    public string Value { get; private set; }

    /// <summary>The user has left this field at least once. Until then an error is computed but
    /// not shown: a form that says "required" into an empty box the moment it opens is a form
    /// shouting at somebody who has not done anything wrong yet.</summary>
    public bool Touched { get; private set; }

    /// <summary>The value differs from <see cref="Initial"/>.</summary>
    public bool Dirty => Value != Initial;

    /// <summary>The first rule that does not hold, or null. Computed on every change, so it is
    /// always current... see <see cref="VisibleError"/> for the one a field may DISPLAY.</summary>
    public string? Error { get; private set; }

    /// <summary>The error the field is allowed to show: an error, once the user has left the field
    /// or the form has been submitted. This is the property a component binds to.</summary>
    public string? VisibleError => Touched ? Error : null;

    public IReadOnlyList<FieldRule> RulesFor => _rules;

    public void AddRule(FieldRule rule)
    {
        _rules.Add(rule);
        Revalidate();
    }

    /// <summary>Types into the field. The error is recomputed immediately but only SURFACES once
    /// the field has been touched, which is what keeps a form quiet while it is being filled in
    /// and honest the moment it has been left.</summary>
    public void Set(string value)
    {
        Value = value;
        Revalidate();
    }

    /// <summary>The user left the field. From here on its error is visible.</summary>
    public void Touch()
    {
        Touched = true;
        Revalidate();
    }

    /// <summary>Back to what the form opened with, untouched — the state a fresh form is in.</summary>
    public void Reset()
    {
        Value = Initial;
        Touched = false;
        Revalidate();
    }

    /// <summary>Adopts the current value as the baseline: nothing is dirty any more, and nothing
    /// is touched. What a successful save does to a form the user stays on.</summary>
    public void Accept()
    {
        Initial = Value;
        Touched = false;
        Revalidate();
    }

    /// <summary>A server's verdict, which no local rule could have known — an email already taken,
    /// a coupon already spent. It shows immediately, because the user has already submitted.</summary>
    public void Fail(string message)
    {
        Error = message;
        Touched = true;
    }

    internal void Reveal() => Touched = true;

    /// <summary>Re-runs this field's rules without changing anything about it — what the form
    /// calls on every OTHER field after a change, because a cross-field rule
    /// (<see cref="Rules.Matches"/>) is an answer about a value that just moved.</summary>
    internal void RevalidateNow() => Revalidate();

    private void Revalidate()
    {
        Error = null;
        foreach (var rule in _rules)
        {
            if (rule.Holds(Value)) continue;
            Error = rule.Message;
            return;
        }
    }
}
