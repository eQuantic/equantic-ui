using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Forms;

/// <summary>
/// Form field wrapper component that provides label, error message, and helper text.
///
/// Usage:
/// <code>
/// new FormField {
///     Label = "Email",
///     Required = true,
///     Error = "Invalid email format",
///     HelperText = "We'll never share your email",
///     Children = {
///         new TextInput {
///             Type = "email",
///             Name = "email"
///         }
///     }
/// }
/// </code>
/// </summary>
public class FormField : StatelessComponent
{
    /// <summary>
    /// Label text for the field
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Error message to display (when validation fails)
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Helper text displayed below the input (when no error)
    /// </summary>
    public string? HelperText { get; set; }

    /// <summary>
    /// Mark field as required (adds asterisk to label)
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// HTML for attribute (links label to input)
    /// </summary>
    public string? For { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var hasError = !string.IsNullOrEmpty(Error);

        var container = new Box
        {
            ClassName = "eq-form-field"
        };

        // Label
        if (!string.IsNullOrEmpty(Label))
        {
            var labelAttrs = new Dictionary<string, string>
            {
                ["class"] = "eq-form-label"
            };

            if (!string.IsNullOrEmpty(For))
            {
                labelAttrs["for"] = For;
            }

            var labelElement = new DynamicElement
            {
                TagName = "label",
                CustomAttributes = labelAttrs
            };

            labelElement.Children.Add(new Text(Label));

            if (Required)
            {
                labelElement.Children.Add(new DynamicElement
                {
                    TagName = "span",
                    InnerText = " *",
                    CustomAttributes = new Dictionary<string, string>
                    {
                        ["class"] = "eq-form-required",
                        ["aria-label"] = "required"
                    }
                });
            }

            container.Children.Add(labelElement);
        }

        // Input wrapper with error class
        var inputWrapper = new Box
        {
            ClassName = hasError ? "eq-form-input-wrapper eq-error" : "eq-form-input-wrapper"
        };

        foreach (var child in this.Children)
        {
            inputWrapper.Children.Add(child);
        }

        container.Children.Add(inputWrapper);

        // Error message or helper text
        if (hasError)
        {
            container.Children.Add(new DynamicElement
            {
                TagName = "span",
                CustomAttributes = new Dictionary<string, string>
                {
                    ["class"] = "eq-error-message",
                    ["role"] = "alert"
                },
                Children = {
                    new Text(Error)
                }
            });
        }
        else if (!string.IsNullOrEmpty(HelperText))
        {
            container.Children.Add(new DynamicElement
            {
                TagName = "span",
                CustomAttributes = new Dictionary<string, string>
                {
                    ["class"] = "eq-helper-text"
                },
                Children = {
                    new Text(HelperText)
                }
            });
        }

        return container;
    }
}
