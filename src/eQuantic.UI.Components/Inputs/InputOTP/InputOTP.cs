using System;
using System.Collections.Generic;
using System.Linq;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Inputs.InputOTP;

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

        var container = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = $"{containerClass} {ClassName}".Trim(),
                ["role"] = "group",
                ["aria-label"] = AriaLabel,
                ["data-otp-id"] = id,
                ["data-otp-length"] = Length.ToString()
            }
        };

        if (Disabled) container.CustomAttributes["data-disabled"] = "true";

        // Hidden input for form submission
        var hiddenAttrs = new Dictionary<string, string>
        {
            ["type"] = "hidden",
            ["value"] = Value ?? ""
        };
        if (!string.IsNullOrEmpty(Name)) hiddenAttrs["name"] = Name;
        container.Children.Add(new DynamicElement
        {
            TagName = "input",
            CustomAttributes = hiddenAttrs
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

        var group = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = groupClass,
                ["role"] = "group"
            }
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

            var slotAttrs = new Dictionary<string, string>
            {
                ["class"] = slotClass,
                ["data-slot-index"] = index.ToString()
            };

            var slot = new DynamicElement
            {
                TagName = "div",
                CustomAttributes = slotAttrs
            };

            // Individual input for each slot
            var inputAttrs = new Dictionary<string, string>
            {
                ["type"] = "text",
                ["inputmode"] = "numeric",
                ["pattern"] = Pattern,
                ["maxlength"] = "1",
                ["value"] = displayValue,
                ["class"] = "eq-otp-input",
                ["autocomplete"] = "one-time-code",
                ["aria-label"] = $"Digit {index + 1}"
            };
            if (Disabled) inputAttrs["disabled"] = "true";

            slot.Children.Add(new DynamicElement
            {
                TagName = "input",
                CustomAttributes = inputAttrs
            });

            // Caret indicator for active slot
            if (isActive && !Disabled)
            {
                var caretClass = OTPTheme?.Caret ?? "eq-otp-caret";
                slot.Children.Add(new DynamicElement
                {
                    TagName = "div",
                    CustomAttributes = new Dictionary<string, string>
                    {
                        ["class"] = caretClass
                    },
                    Children = {
                        new DynamicElement
                        {
                            TagName = "div",
                            CustomAttributes = new Dictionary<string, string>
                            {
                                ["class"] = "eq-otp-caret-line"
                            }
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

        return new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = separatorClass,
                ["role"] = "separator",
                ["aria-orientation"] = "vertical"
            },
            Children = { new Text("–") }
        };
    }
}
