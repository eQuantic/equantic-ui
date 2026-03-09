namespace eQuantic.UI.Core.Theme;

/// <summary>
/// Theme interface for InputOTP component styling.
/// </summary>
public interface IInputOTPTheme
{
    /// <summary>CSS classes for the OTP input container.</summary>
    string Container { get; }

    /// <summary>CSS classes for a group of OTP slots.</summary>
    string Group { get; }

    /// <summary>CSS classes for an individual OTP slot.</summary>
    string Slot { get; }

    /// <summary>CSS classes for the currently focused/active slot.</summary>
    string ActiveSlot { get; }

    /// <summary>CSS classes for the separator between groups.</summary>
    string Separator { get; }

    /// <summary>CSS classes for the caret/cursor indicator inside a slot.</summary>
    string Caret { get; }
}
