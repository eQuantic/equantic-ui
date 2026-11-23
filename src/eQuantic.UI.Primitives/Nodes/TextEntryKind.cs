namespace eQuantic.UI.Primitives;

/// <summary>
/// What a <see cref="TextEntry"/> takes. The field says what it holds, and each target derives the
/// rest from it: the keyboard a phone opens, the autofill a browser or a platform offers, and whether
/// the text is prose to spell-check and autocorrect. Flutter's <c>TextInputType</c>, named for what
/// the field takes rather than for a keyboard a desktop does not have.
/// </summary>
public enum TextEntryKind : byte
{
    /// <summary>Prose: the text keyboard, with spelling and autocorrection.</summary>
    Text = 0,

    /// <summary>An e-mail address: the keyboard with <c>@</c>, and the e-mail autofill.</summary>
    Email = 1,

    /// <summary>A telephone number: the telephone keypad, and the telephone autofill.</summary>
    Phone = 2,

    /// <summary>A web address: the keyboard with <c>/</c> and <c>.</c>, and the URL autofill.</summary>
    Url = 3,

    /// <summary>A whole number: the digits.</summary>
    Number = 4,

    /// <summary>A number with a fraction: the digits and the decimal separator.</summary>
    Decimal = 5,
}
