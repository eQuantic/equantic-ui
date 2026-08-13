using eQuantic.UI.Components;
using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The form surface on the web. The model is the same C# the server validates with and the browser
/// re-validates with (its transpiled twin), so what this file pins is the RENDERED half: the value
/// a field carries into the markup, the error the SSR page is allowed to show, and the one it is
/// not.
/// <para>
/// The last part is the one worth a test rather than a comment. A server-rendered form arrives with
/// no user having touched anything, so it must arrive QUIET — a page whose required fields are red
/// before the reader has typed is a page that looks broken on first paint, and hydration cannot
/// take that back.
/// </para>
/// </summary>
public class FormRealizerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, Theme).Render();

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    private static string TextOf(HtmlNode node) =>
        string.Concat(Walk(node).Select(child => child.TextContent ?? ""));

    private static FormController SignUp()
    {
        var form = new FormController();
        form.Add("email", rules: [Rules.Required(), Rules.Email()]);
        form.Add("password", rules: [Rules.Required(), Rules.MinLength(8)]);
        return form;
    }

    [Fact]
    public void AFieldCarriesItsValueAndItsLabelIntoTheMarkup()
    {
        var form = SignUp();
        form.Set("email", "ana@equantic.tech");

        var html = Render(new FormInput(form, "email", "Email", placeholder: "you@example.com"));

        var input = Walk(html).Single(node => node.Tag == "input");
        input.Attributes["value"].Should().Be("ana@equantic.tech");
        input.Attributes["placeholder"].Should().Be("you@example.com");
        TextOf(html).Should().Contain("Email");
    }

    /// <summary>
    /// The SSR form arrives quiet. Every rule has already run — the model knows the email is
    /// required — and nothing says so, because nobody has touched anything yet.
    /// </summary>
    [Fact]
    public void AServerRenderedFormDoesNotArriveShouting()
    {
        var form = SignUp();

        var html = Render(new FormInput(form, "email", "Email", helper: "We never share it"));

        form.Valid.Should().BeFalse("the model knows the field is required");
        TextOf(html).Should().Contain("We never share it");
        TextOf(html).Should().NotContain("This field is required.");
    }

    /// <summary>Once the field has been left, the same render says what is wrong — and the entry
    /// tells assistive tech, which is the half a red border cannot carry.</summary>
    [Fact]
    public void ATouchedFieldRendersItsErrorAndSaysSoToAssistiveTech()
    {
        var form = SignUp();
        form.Set("email", "ana@");
        form.Touch("email");

        var html = Render(new FormInput(form, "email", "Email", helper: "We never share it"));

        TextOf(html).Should().Contain("Enter a valid email address.");
        TextOf(html).Should().NotContain("We never share it", "the error replaces the helper");
        Walk(html).Single(node => node.Tag == "input")
            .Attributes["aria-invalid"].Should().Be("true");
    }

    /// <summary>
    /// The submit button stays PRESSABLE while the form is invalid, and that is the design: a
    /// disabled submit is the most common way to make a form feel broken, because the field that
    /// is wrong is usually one the user never visited. Pressing it is what reveals the answer.
    /// </summary>
    [Fact]
    public void TheSubmitButtonIsPressableEvenWhileTheFormIsInvalid()
    {
        var form = SignUp();

        var html = Render(new FormSubmit(form, "Create account", () => Task.CompletedTask));

        var button = Walk(html).First(node => node.Tag == "button");
        button.Attributes.Should().NotContainKey("disabled");
        TextOf(html).Should().Contain("Create account");
    }

    /// <summary>While a submit is in flight the button says so, and the fields stop accepting
    /// edits — a form that takes changes mid-save saves something the user did not see.</summary>
    [Fact]
    public void WhileSubmittingTheFieldsAreHeld()
    {
        var form = SignUp();
        form.Set("email", "ana@equantic.tech");
        form.Set("password", "correcthorse");
        var gate = new TaskCompletionSource();
        _ = form.SubmitAsync(() => gate.Task);

        var html = Render(new FormInput(form, "email", "Email"));

        form.Submitting.Should().BeTrue();
        Walk(html).Single(node => node.Tag == "input").Attributes.Should().ContainKey("disabled");

        gate.SetResult();
    }
}
