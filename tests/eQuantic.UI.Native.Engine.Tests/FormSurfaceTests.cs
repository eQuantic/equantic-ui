using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The form model wired to real pixels, driven by a real keyboard.
/// <para>
/// <see cref="FormModelTests"/> proves the model in isolation, which is the point of keeping it
/// pixel-free... but a model nobody has connected is a model that does not work. This drives the
/// window the way a person does — Tab, type, Tab away — and asserts the FIELD's own state changed,
/// because the wires between the two are the only thing this file is about.
/// </para>
/// </summary>
public class FormSurfaceTests
{
    private sealed class SignUpPage : StatefulComponent
    {
        public readonly FormController Form = new();
        public int Submits;
        public TaskCompletionSource? Gate;

        public SignUpPage()
        {
            Form.Add("email", rules: [Rules.Required(), Rules.Email()]);
            Form.Add("password", rules: [Rules.Required(), Rules.MinLength(8)]);
            // One subscription, every change — what a page owes the controller it holds.
            Form.Changed += () => SetState(() => { });
        }

        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: Space.S3) { Width = SizeValue.Fill };
            column.Add(new FormInput(Form, "email", "Email", placeholder: "you@example.com"));
            column.Add(new FormInput(Form, "password", "Password", helper: "At least 8 characters")
                { Obscure = true });
            column.Add(new FormSubmit(Form, "Create account", async () =>
            {
                Submits++;
                if (Gate is not null) await Gate.Task;
            }));
            return column;
        }
    }

    private static (PhotonHost Host, SignUpPage Page) Open()
    {
        var page = new SignUpPage();
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 480, 400);
        host.RenderFrame(new DisplayListBuilder());
        return (host, page);
    }

    private static void Press(PhotonHost host, string key)
    {
        host.KeyDown(key, KeyModifiers.None);
        host.RenderFrame(new DisplayListBuilder());
    }

    private static void Type(PhotonHost host, string text)
    {
        foreach (var character in text)
        {
            host.TextInput(character.ToString());
            host.RenderFrame(new DisplayListBuilder());
        }
    }

    private static IReadOnlyList<string> TextOf(PhotonHost host) =>
        [.. host.Semantics().Select(node => node.Label).Where(label => !string.IsNullOrEmpty(label))!];

    /// <summary>Typing reaches the model — the first wire.</summary>
    [Fact]
    public void TypingReachesTheField()
    {
        var (host, page) = Open();

        Press(host, "Tab");
        Type(host, "ana@equantic.tech");

        page.Form.Field("email")!.Value.Should().Be("ana@equantic.tech");
        page.Form.Field("email")!.Dirty.Should().BeTrue();
    }

    /// <summary>
    /// The second wire, and the one that makes a form feel considerate: an error appears when you
    /// LEAVE a field, not while you are still in it. Tab in, type something incomplete, Tab out.
    /// </summary>
    [Fact]
    public void LeavingAFieldIsWhatRevealsItsError()
    {
        var (host, page) = Open();

        Press(host, "Tab");
        Type(host, "ana@");

        var email = page.Form.Field("email")!;
        email.Error.Should().Be("Enter a valid email address.");
        email.VisibleError.Should().BeNull("the caret is still in the box");
        TextOf(host).Should().NotContain("Enter a valid email address.");

        Press(host, "Tab");   // focus moves to the password field: email has been left

        email.VisibleError.Should().Be("Enter a valid email address.");
        TextOf(host).Should().Contain("Enter a valid email address.",
            "the field draws what the model finally allows it to say");
    }

    /// <summary>
    /// Pressing submit on an invalid form is not a no-op. It reveals the fields the user never
    /// visited — the complaint every validated form earns, answered by the controller.
    /// </summary>
    [Fact]
    public void SubmittingAnUntouchedFormMakesItSpeak()
    {
        var (host, page) = Open();

        // Tab to the button past both fields, and press it without having typed anything.
        Press(host, "Tab");
        Press(host, "Tab");
        Press(host, "Tab");
        Press(host, "Enter");
        host.RenderFrame(new DisplayListBuilder());

        page.Submits.Should().Be(0, "an invalid form never reaches the handler");
        TextOf(host).Should().Contain("This field is required.");
    }

    /// <summary>A valid form submits, and the button says it is working while it does.</summary>
    [Fact]
    public async Task AValidFormSubmits_AndTheButtonSaysSoWhileItRuns()
    {
        var (host, page) = Open();
        page.Gate = new TaskCompletionSource();

        Press(host, "Tab");
        Type(host, "ana@equantic.tech");
        Press(host, "Tab");
        Type(host, "correcthorse");
        Press(host, "Tab");
        Press(host, "Enter");
        host.RenderFrame(new DisplayListBuilder());

        page.Submits.Should().Be(1);
        page.Form.Submitting.Should().BeTrue();
        // The fields are held while the submit is in flight: a form that accepts edits mid-save
        // is a form that saves something the user did not see.
        page.Form.Field("email")!.Value.Should().Be("ana@equantic.tech");

        page.Gate.SetResult();
        await Task.Delay(20);
        host.RenderFrame(new DisplayListBuilder());

        page.Form.Submitting.Should().BeFalse();
    }

    /// <summary>A field bound to a name nobody declared draws nothing instead of taking the page
    /// down — the form is usually built a few lines above, and a typo there is survivable.</summary>
    [Fact]
    public void AFieldBoundToANameThatDoesNotExistDrawsNothing()
    {
        var form = new FormController();
        var node = new FormInput(form, "nope", "Nope");

        var host = new PhotonHost(node, PhotonTheme.Instance, ThemeMode.Light, 300, 200);
        var render = () => host.RenderFrame(new DisplayListBuilder());

        render.Should().NotThrow();
        TextOf(host).Should().NotContain("Nope");
    }
}
