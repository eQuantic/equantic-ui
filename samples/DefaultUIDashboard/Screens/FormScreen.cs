using eQuantic.UI.Components;
using eQuantic.UI.Core;
using eQuantic.UI.Primitives;

namespace eQuantic.Console;

/// <summary>
/// A form, end to end: the model from <c>Primitives</c>, the fields bound to it, and a submit that
/// goes to the server and comes back with a verdict this page could not have known.
///
/// The page holds a <see cref="FormController"/> and subscribes ONCE — every value, flag and error
/// arrives through that one event, so nothing here tracks state a second time. What is worth
/// watching in the browser is the timing: type a broken email and nothing turns red, leave the
/// field and it does, fix it and the red goes on the keystroke that fixes it.
/// </summary>
[Page("/form", Title = "Form — eQuantic Console")]
public sealed class FormScreen : StatefulComponent
{
    private readonly FormController _form = new();
    private string? _created;

    /// <summary>Page state, not form state — and the condition the phone field reads. Which is the
    /// point: a condition is ordinary C# that happens to be true or false right now.</summary>
    private bool _callMe;

    public FormScreen()
    {
        _form.Add("email", rules: [Rules.Required(), Rules.Email()]);
        var password = _form.Add("password", rules: [Rules.Required(), Rules.MinLength(8)]);
        _form.Add("confirm", rules: [Rules.Required(), Rules.Matches(password)]);
        // Conditional: the phone is not a field with a passing rule while nobody asked to be
        // called, it is a field that is NOT BEING ASKED — no error, no vote on whether the form
        // is valid, and the page does not draw it.
        _form.Add("phone", relevantWhen: () => _callMe,
            rules: [Rules.Required("We need a number to call you on."), Rules.MinLength(9)]);
        _form.Changed += () => SetState(() => { });
    }

    /// <summary>
    /// The only validation that counts. The client's rules are a courtesy — this one runs where
    /// the data lives, and its verdict comes back onto the field it belongs to.
    /// </summary>
    [ServerAction]
    public async Task<List<FieldError>> Register(string email)
    {
        await Task.Delay(400);   // the round trip a real registration would take
        return email.Trim().ToLowerInvariant() == "ana@equantic.tech"
            ? [new FieldError("email", "That address is already registered.")]
            : [];
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var column = new Column(gap: Space.S4) { Width = SizeValue.Fill, Cross = CrossAlign.Start };

        // `Assets/mark.svg`, read at BUILD time into `Vectors.Mark` — the same drawing data the
        // browser gets as inline <svg> and a native window gets as GPU pixels. The tint answers
        // ONLY the two shapes the file left as currentColor, and it is a fixed light ink rather
        // than a theme token on purpose: the plate under them is part of the artwork and is dark
        // in both modes, so a token that flips would make the wordmark vanish in one of them.
        column.Add(new Drawing(Vectors.Mark, 240,
            tint: new ColorToken(Color.FromRgb(224, 231, 255)), label: "eQuantic"));
        column.Add(new Text("Create your account", TypeRole.Heading, theme.TextPrimary));
        column.Add(new Text(
            "Type a broken address and nothing turns red. Leave the field and it does. "
            + "Try ana@equantic.tech to see the server disagree with a valid-looking form.",
            TypeRole.BodyM, theme.TextSecondary, maxLines: 3));

        var card = new Column(gap: Space.S3) { Width = SizeValue.Fill };
        card.Add(new FormInput(_form, "email", "Email", placeholder: "you@example.com",
            helper: "We never share it."));
        card.Add(new FormInput(_form, "password", "Password", helper: "At least 8 characters")
            { Obscure = true });
        card.Add(new FormInput(_form, "confirm", "Confirm password") { Obscure = true });

        // The conditional half. The checkbox is page state, so the form is TOLD it changed —
        // the one case a value moving inside the form does not cover.
        card.Add(new Checkbox(_callMe, () => SetState(() =>
        {
            _callMe = !_callMe;
            _form.Revalidate();
        }), "Call me instead of emailing"));

        if (_form.Field("phone") is { Relevant: true })
            card.Add(new FormInput(_form, "phone", "Phone", placeholder: "+351 912 345 678"));

        if (_form.SubmitError is { } failure)
            card.Add(new Banner(Variant.Destructive, failure));

        if (_created is { } created)
            card.Add(new Banner(Variant.Success, $"Welcome, {created}."));

        var actions = new Row(gap: Space.S2) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        actions.Add(new FormSubmit(_form, "Create account", Submit));
        actions.Add(new Button("Reset", Variant.Ghost) { OnPressed = () => _form.Reset() });
        card.Add(actions);

        column.Add(new Box(new BoxStyle
        {
            Width = SizeValue.Fixed(420),
            Padding = EdgeInsets.All(Space.S4),
            Background = theme.Surface,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Large)),
            BorderWidth = 1,
            BorderColor = theme.Border,
        }, card));

        return new Box(new BoxStyle { Width = SizeValue.Fill, Padding = EdgeInsets.All(Space.S5) }, column);
    }

    /// <summary>
    /// The submit itself, which is ordinary C#: await the server action, and put whatever it says
    /// back onto the form. The controller has already refused an invalid form and is already
    /// holding the fields, so neither concern appears here.
    /// </summary>
    private async Task Submit()
    {
        var email = _form.Field("email")!.Value;
        var verdict = await Register(email);
        if (verdict.Count > 0)
        {
            _form.ApplyServerErrors(verdict);
            return;
        }

        _created = email;
        _form.Accept();
    }
}
