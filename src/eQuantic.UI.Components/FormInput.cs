using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// A <see cref="TextInput"/> bound to a form field — the seam between the model and the pixels.
/// <para>
/// It is deliberately thin, and thin is the design: everything about how a form BEHAVES lives in
/// <see cref="FormController"/>, where it can be tested without a screen, and everything about how
/// a field LOOKS already lives in TextInput. This node only knows which of the two owns what.
/// </para>
/// <para>
/// The three wires are the whole story. Typing calls <see cref="FormController.Set"/>, so
/// cross-field rules re-run. LEAVING calls <see cref="FormController.Touch"/>, which is the moment
/// a form is allowed to say what is wrong. And the error drawn is <see cref="FormField.VisibleError"/>,
/// never <c>Error</c> — the difference between the two is why a form does not shout at somebody
/// who has not typed yet.
/// </para>
/// </summary>
public sealed class FormInput : StatelessComponent
{
    public FormInput(FormController form, string name, string label = "",
        string? placeholder = null, string? helper = null, Icons? leading = null,
        SizeVariant size = SizeVariant.Large)
    {
        Form = form;
        Name = name;
        Label = label;
        Placeholder = placeholder;
        Helper = helper;
        Leading = leading;
        Size = size;
    }

    public FormController Form { get; init; }

    /// <summary>Which field of the form this draws. A name rather than the field itself, because a
    /// form rebuilds and the name is the part that does not change.</summary>
    public string Name { get; init; }

    public string Label { get; init; }
    public string? Placeholder { get; init; }

    /// <summary>The hint under the field while nothing is wrong. The error replaces it when there
    /// is one, and the line is always reserved, so a swap never shifts the form.</summary>
    public string? Helper { get; init; }

    public Icons? Leading { get; init; }
    public SizeVariant Size { get; init; }
    public bool Obscure { get; init; }
    public bool Autofocus { get; init; }
    public bool Disabled { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var field = Form.Field(Name);
        // A name nobody declared draws nothing rather than taking the page down. The form is
        // usually built a few lines above this call, and a typo there is a rename away from being
        // correct — a blank field is a visible, survivable answer.
        if (field is null) return new Box();

        return new TextInput(field.Value, value => Form.Set(Name, value), Label,
            Placeholder, Helper, field.VisibleError, Leading, Size)
        {
            Obscure = Obscure,
            Autofocus = Autofocus,
            Disabled = Disabled || Form.Submitting,
            // Leaving the field is what reveals its error. Arriving is not: a form that turned red
            // as your caret entered the box would be shouting before you had a chance to type.
            OnFocusChanged = focused =>
            {
                if (!focused) Form.Touch(Name);
            },
        };
    }
}

/// <summary>
/// The button that submits a form. It reads the controller for everything it needs, so no page has
/// to remember the three rules it would otherwise re-implement: it spins while a submit is in
/// flight, it refuses to be pressed twice, and it stays PRESSABLE while the form is invalid.
/// <para>
/// That last one is a choice worth defending. A disabled submit button is the most common way to
/// make a form feel broken: the user cannot tell WHY it is disabled, and the field that is wrong is
/// usually one they never visited. Pressing it is what reveals the answer — the controller touches
/// every field, and the form finally says what it wants.
/// </para>
/// </summary>
public sealed class FormSubmit : StatelessComponent
{
    public FormSubmit(FormController form, string label, Func<Task> onSubmit,
        Variant variant = Variant.Primary, SizeVariant size = SizeVariant.Medium)
    {
        Form = form;
        Label = label;
        OnSubmit = onSubmit;
        Variant = variant;
        Size = size;
    }

    public FormController Form { get; init; }
    public string Label { get; init; }
    public Func<Task> OnSubmit { get; init; }
    public Variant Variant { get; init; }
    public SizeVariant Size { get; init; }

    /// <summary>Fills the row it sits in — what a dialog's confirm button does.</summary>
    public bool Wide { get; init; }

    public override VisualNode Build(ComponentContext context) =>
        new Button(Label, Variant, Size)
        {
            Loading = Form.Submitting,
            Expand = Wide,
            OnPressed = () => _ = Form.SubmitAsync(OnSubmit),
        };
}
