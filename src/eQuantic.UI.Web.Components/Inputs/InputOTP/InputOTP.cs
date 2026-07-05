using System;
using System.Collections.Generic;
using System.Linq;
using eQuantic.UI.Core;

namespace eQuantic.UI.Web.Components.Inputs.InputOTP;

/// <summary>
/// One-time password input with individual character slots.
/// Each character is typed in its own slot, auto-advancing to the next.
///
/// Usage:
/// <code>
/// new InputOTP {
///     Length = 6,
///     Children = {
///         new InputOTPGroup { Slots = 3 },
///         new InputOTPSeparator(),
///         new InputOTPGroup { Slots = 3 }
///     }
/// }
/// </code>
///
/// Or with flat slots:
/// <code>
/// new InputOTP { Length = 4 }
/// </code>
/// </summary>
public class InputOTP : StatelessComponent
{
    /// <summary>Number of OTP digits/characters. Default: 6.</summary>
    public int Length { get; set; } = 6;

    /// <summary>Current OTP value.</summary>
    public string? Value { get; set; }

    /// <summary>Name for form submission.</summary>
    public string? Name { get; set; }

    /// <summary>Whether the input is disabled. Default: false.</summary>
    public bool Disabled { get; set; }

    /// <summary>
    /// Whether to mask the input (show dots instead of characters). Default: false.
    /// </summary>
    public bool Masked { get; set; }

    /// <summary>
    /// Pattern regex for allowed characters. Default: "[0-9]" (digits only).
    /// </summary>
    public string Pattern { get; set; } = "[0-9]";

    /// <summary>Accessible label for the input group.</summary>
    public string AriaLabel { get; set; } = "One-time password";

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var otpTheme = theme?.InputOTP;

        var containerClass = otpTheme?.Container ?? "eq-otp";
        var paddedValue = (Value ?? "").PadRight(Length);
        var id = Id ?? $"otp-{Guid.NewGuid():N}";

        var container = new Box
        {
            As = "div",
            ClassName = $"{containerClass} {ClassName}".Trim(),
            Role = "group",
            AriaLabel = AriaLabel,
            DataAttributes = new Dictionary<string, string>
            {
                ["otp-id"] = id,
                ["otp-length"] = Length.ToString()
            }
        };

        if (Disabled) container.DataAttributes["disabled"] = "true";

        // Hidden input for form submission
        var hiddenAttrs = new Dictionary<string, string>
        {
            ["type"] = "hidden",
            ["value"] = Value ?? ""
        };
        if (!string.IsNullOrEmpty(Name)) hiddenAttrs["name"] = Name;
        container.Children.Add(new Box
        {
            As = "input",
            Type = "hidden",
            Value = Value ?? "",
            Name = Name
        });

        if (Children.Any())
        {
            // Compound pattern: user provided InputOTPGroup/InputOTPSeparator children
            var slotIndex = 0;
            foreach (var child in Children)
            {
                if (child is InputOTPGroup group)
                {
                    group.OTPTheme = otpTheme;
                    group.StartIndex = slotIndex;
                    group.PaddedValue = paddedValue;
                    group.Masked = Masked;
                    group.Disabled = Disabled;
                    group.Pattern = Pattern;
                    group.OTPId = id;
                    slotIndex += group.Slots;
                }
                else if (child is InputOTPSeparator separator)
                {
                    separator.OTPTheme = otpTheme;
                }
                container.Children.Add(child);
            }
        }
        else
        {
            // Auto-generate a single group with all slots
            var group = new InputOTPGroup
            {
                Slots = Length,
                OTPTheme = otpTheme,
                StartIndex = 0,
                PaddedValue = paddedValue,
                Masked = Masked,
                Disabled = Disabled,
                Pattern = Pattern,
                OTPId = id
            };
            container.Children.Add(group);
        }

        return container;
    }
}

/// <summary>
/// A group of OTP input slots.
/// </summary>
public class InputOTPGroup : StatelessComponent
{
    /// <summary>Number of slots in this group. Default: 3.</summary>
    public int Slots { get; set; } = 3;

    // Internal properties set by parent InputOTP
    internal Core.Theme.IInputOTPTheme? OTPTheme { get; set; }
    internal int StartIndex { get; set; }
    internal string PaddedValue { get; set; } = "";
    internal bool Masked { get; set; }
    internal bool Disabled { get; set; }
    internal string Pattern { get; set; } = "[0-9]";
    internal string OTPId { get; set; } = "";

    public override IComponent Build(RenderContext context)
    {
        var groupClass = OTPTheme?.Group ?? "eq-otp-group";

        var group = new Box
        {
            As = "div",
            ClassName = groupClass,
            Role = "group"
        };

        for (var i = 0; i < Slots; i++)
        {
            var index = StartIndex + i;
            var charValue = index < PaddedValue.Length ? PaddedValue[index] : ' ';
            var hasValue = charValue != ' ';
            var displayValue = hasValue ? (Masked ? "●" : charValue.ToString()) : "";

            var slotClass = OTPTheme?.Slot ?? "eq-otp-slot";
            var isActive = hasValue && (index == (PaddedValue.TrimEnd().Length)); // Next empty slot
            if (isActive)
            {
                var activeClass = OTPTheme?.ActiveSlot ?? "eq-otp-active";
                slotClass = $"{slotClass} {activeClass}";
            }

            var slot = new Box
        {
            As = "div",
            ClassName = slotClass,
            DataAttributes = new Dictionary<string, string> { ["slot-index"] = index.ToString() }
        };

            slot.Children.Add(new Box
            {
                As = "input",
                Type = "text",
                ClassName = "eq-otp-input",
                Value = displayValue,
                Disabled = Disabled,
                InputMode = "numeric",
                Pattern = Pattern,
                MaxLength = 1,
                Autocomplete = "one-time-code",
                AriaLabel = $"Digit {index + 1}"
            });

            // Caret indicator for active slot
            if (isActive && !Disabled)
            {
                var caretClass = OTPTheme?.Caret ?? "eq-otp-caret";
                slot.Children.Add(new Box
                {
                    As = "div",
                    ClassName = caretClass,
                    Children = {
                        new Box
                        {
                            As = "div",
                            ClassName = "eq-otp-caret-line"
                        }
                    }
                });
            }

            group.Children.Add(slot);
        }

        return group;
    }
}

/// <summary>
/// A visual separator between OTP groups (e.g., a dash between groups of 3).
/// </summary>
public class InputOTPSeparator : StatelessComponent
{
    internal Core.Theme.IInputOTPTheme? OTPTheme { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var separatorClass = OTPTheme?.Separator ?? "eq-otp-separator";

        return new Box
        {
            As = "div",
            ClassName = separatorClass,
            Role = "separator",
            AriaOrientation = "vertical",
            Children = { new Text("–") }
        };
    }
}
