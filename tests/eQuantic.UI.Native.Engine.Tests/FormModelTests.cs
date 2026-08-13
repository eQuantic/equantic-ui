using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The form model — no pixels, which is the whole point. A form is mostly arithmetic about state
/// (has this been left yet, does this differ from what we opened with, may this submit), and
/// arithmetic wired into a widget can only be tested by clicking.
/// <para>
/// The behaviours pinned here are the ones users complain about when a framework gets them wrong:
/// a form that shouts before you have typed, a form whose button silently does nothing, and a form
/// that submits twice because the button was pressed twice.
/// </para>
/// </summary>
public class FormModelTests
{
    private static FormController SignUp()
    {
        var form = new FormController();
        form.Add("email", rules: [Rules.Required(), Rules.Email()]);
        var password = form.Add("password", rules: [Rules.Required(), Rules.MinLength(8)]);
        form.Add("confirm", rules: [Rules.Required(), Rules.Matches(password)]);
        return form;
    }

    // ---- what a field says, and when ------------------------------------------------------------

    /// <summary>
    /// The rule every good form follows and few state: an error is COMPUTED immediately and SHOWN
    /// only once the user has left the field. A form that says "required" into an empty box the
    /// moment it opens is shouting at somebody who has not done anything wrong yet.
    /// </summary>
    [Fact]
    public void AFieldDoesNotShoutBeforeItHasBeenLeft()
    {
        var form = SignUp();
        var email = form.Field("email")!;

        email.Error.Should().NotBeNull("an empty required field is invalid from the start");
        email.VisibleError.Should().BeNull("...and says nothing about it yet");

        email.Touch();

        email.VisibleError.Should().Be("This field is required.");
    }

    /// <summary>Once touched, it corrects itself live — the other half of the same rule. Waiting
    /// for a second blur to admit the value is now fine is how a form feels stuck.</summary>
    [Fact]
    public void OnceTouchedItFollowsEveryKeystroke()
    {
        var form = SignUp();
        form.Touch("email");
        form.Set("email", "ana@");

        form.Field("email")!.VisibleError.Should().Be("Enter a valid email address.");

        form.Set("email", "ana@equantic.tech");

        form.Field("email")!.VisibleError.Should().BeNull("the value is valid now, so the error is gone");
    }

    /// <summary>
    /// Touched and dirty are different questions. Tabbing through a field touches it without
    /// changing anything... a form that called that dirty would prompt "discard changes?" at
    /// somebody who changed nothing.
    /// </summary>
    [Fact]
    public void TouchedIsNotDirty()
    {
        var form = new FormController();
        var name = form.Add("name", "Ana");

        name.Touch();

        name.Touched.Should().BeTrue();
        name.Dirty.Should().BeFalse("the value is what it opened with");
        form.Dirty.Should().BeFalse();

        name.Set("Ana Beatriz");
        form.Dirty.Should().BeTrue();

        name.Set("Ana");
        form.Dirty.Should().BeFalse("typing back to the original is not a change");
    }

    // ---- the rules -------------------------------------------------------------------------------

    /// <summary>
    /// Every rule but Required passes on an EMPTY value, because an optional field left blank is
    /// absent, not badly formatted. "Enter a valid email" under an empty optional box is the most
    /// common false alarm in form validation.
    /// </summary>
    [Fact]
    public void AnOptionalFieldIsNotBadlyFormattedWhenItIsEmpty()
    {
        var form = new FormController();
        var website = form.Add("website", rules: [Rules.Email(), Rules.MinLength(5), Rules.Range(1, 10)]);

        website.Touch();

        website.VisibleError.Should().BeNull("blank and optional is a complete answer");
    }

    [Theory]
    [InlineData("ana@equantic.tech", true)]
    [InlineData("ana.b@sub.equantic.tech", true)]
    [InlineData("ana@equantic", false)]      // no dot in the domain
    [InlineData("ana@@equantic.tech", false)] // two @
    [InlineData("@equantic.tech", false)]     // nothing before the @
    [InlineData("ana@equantic.", false)]      // nothing after the dot
    [InlineData("an a@equantic.tech", false)] // a space in the local part
    public void TheEmailRuleAcceptsWhatAProviderWouldIssue(string value, bool valid)
    {
        var form = new FormController();
        var email = form.Add("email", rules: [Rules.Email()]);

        email.Set(value);

        (email.Error is null).Should().Be(valid);
    }

    /// <summary>A cross-field rule reads the OTHER field at validation time, so it is always
    /// judged against what is on screen — not against what was there when the form was built.</summary>
    [Fact]
    public void MatchesReadsTheOtherFieldWhenItRuns()
    {
        var form = SignUp();
        form.Set("password", "correcthorse");
        form.Set("confirm", "correcthorse");
        form.Field("confirm")!.Error.Should().BeNull();

        form.Set("password", "correcthorsebattery");

        form.Field("confirm")!.Error.Should().Be("The two values do not match.",
            "the confirmation is judged against the password as it is NOW");
    }

    // ---- submitting ------------------------------------------------------------------------------

    /// <summary>
    /// The complaint every validated form earns: you press the button and nothing happens, because
    /// the thing that is wrong is a field you never visited. Submitting reveals every error.
    /// </summary>
    [Fact]
    public async Task AnInvalidSubmitRevealsWhatIsWrongInsteadOfDoingNothing()
    {
        var form = SignUp();
        form.Set("email", "ana@equantic.tech");
        var ran = false;

        var ok = await form.SubmitAsync(() => { ran = true; return Task.CompletedTask; });

        ok.Should().BeFalse();
        ran.Should().BeFalse("the handler never sees an invalid form");
        form.Field("password")!.VisibleError.Should().Be("This field is required.",
            "a field the user never visited now says what is wrong with it");
    }

    /// <summary>The double click that charges a card twice. The second call is refused while the
    /// first is in flight, by the controller, so no caller has to remember.</summary>
    [Fact]
    public async Task ASecondSubmitIsRefusedWhileTheFirstIsInFlight()
    {
        var form = Filled();
        var gate = new TaskCompletionSource();
        var runs = 0;

        var first = form.SubmitAsync(async () => { runs++; await gate.Task; });
        var second = await form.SubmitAsync(() => { runs++; return Task.CompletedTask; });

        second.Should().BeFalse("one submit at a time");
        form.Submitting.Should().BeTrue();

        gate.SetResult();
        (await first).Should().BeTrue();
        runs.Should().Be(1);
        form.Submitting.Should().BeFalse("the flag clears even though the first submit is what cleared it");
    }

    /// <summary>A network that fails is a thing forms do, not a crash. The throw becomes state the
    /// form can draw.</summary>
    [Fact]
    public async Task AThrowingSubmitBecomesFormState()
    {
        var form = Filled();

        var ok = await form.SubmitAsync(() => throw new InvalidOperationException("Service unavailable"));

        ok.Should().BeFalse();
        form.SubmitError.Should().Be("Service unavailable");
        form.Submitting.Should().BeFalse("the finally runs on the failing path too");
    }

    /// <summary>
    /// The server is the only validator that counts, and this is how its verdict lands back on the
    /// form the user is looking at: on the FIELD it belongs to, visible immediately, because by
    /// then the user has already submitted.
    /// </summary>
    [Fact]
    public void TheServersVerdictLandsOnTheFieldItBelongsTo()
    {
        var form = Filled();

        form.ApplyServerErrors([new FieldError("email", "That address is already registered.")]);

        form.Field("email")!.VisibleError.Should().Be("That address is already registered.");
        form.Valid.Should().BeFalse();
    }

    /// <summary>What a successful save does to a form the user stays on: the values become the new
    /// baseline, so nothing is dirty and nothing is shouting.</summary>
    [Fact]
    public void AcceptingMakesTheCurrentValuesTheBaseline()
    {
        var form = Filled();
        form.Dirty.Should().BeTrue();

        form.Accept();

        form.Dirty.Should().BeFalse();
        form.Fields.Should().OnlyContain(field => !field.Touched);
    }

    /// <summary>One subscription, every change — what a component wires to its own SetState.</summary>
    [Fact]
    public void EveryMutationAnnouncesItself()
    {
        var form = SignUp();
        var beats = 0;
        form.Changed += () => beats++;

        form.Set("email", "a");
        form.Touch("email");
        form.Validate();
        form.Reset();

        beats.Should().Be(4);
    }

    /// <summary>
    /// A rule that only applies under a condition — the ordinary shape of a real form. The invoice
    /// needs a tax number ONLY for a company, and while "personal" is selected the same empty box
    /// is not a problem to be fixed.
    /// </summary>
    [Fact]
    public void AConditionalRuleIsNotAskingWhileItsConditionIsFalse()
    {
        var form = new FormController();
        var kind = form.Add("kind", "personal");
        form.Add("taxNumber", rules: [Rules.When(() => kind.Value == "company", Rules.Required())]);

        form.Valid.Should().BeTrue("a personal invoice is not missing a tax number");

        form.Set("kind", "company");

        form.Valid.Should().BeFalse("the same empty box is now the one thing missing");
        form.Field("taxNumber")!.Error.Should().Be("This field is required.");
    }

    /// <summary>
    /// The other half: a field that is not being ASKED at all. Its rules do not run, it cannot hold
    /// the form invalid, and what it already said stops being said — a red box under a question
    /// that is no longer on screen is the worst of both.
    /// </summary>
    [Fact]
    public void AnIrrelevantFieldHoldsNoErrorAndBlocksNothing()
    {
        var form = new FormController();
        var contact = form.Add("contact", "phone");
        form.Add("phone", relevantWhen: () => contact.Value == "phone",
            rules: [Rules.Required(), Rules.MinLength(9)]);

        form.Set("phone", "123");
        form.Touch("phone");
        form.Field("phone")!.VisibleError.Should().Be("Use at least 9 characters.");
        form.Valid.Should().BeFalse();

        form.Set("contact", "email");   // the question goes away

        form.Field("phone")!.Relevant.Should().BeFalse();
        form.Field("phone")!.Error.Should().BeNull();
        form.Field("phone")!.VisibleError.Should().BeNull();
        form.Valid.Should().BeTrue();
    }

    /// <summary>What was typed survives the question going away and coming back. Losing it is the
    /// kind of small betrayal that makes people distrust a form.</summary>
    [Fact]
    public void AFieldThatComesBackStillHasWhatWasTypedInIt()
    {
        var form = new FormController();
        var contact = form.Add("contact", "phone");
        form.Add("phone", relevantWhen: () => contact.Value == "phone", rules: [Rules.Required()]);
        form.Set("phone", "+351 912 345 678");

        form.Set("contact", "email");
        form.Set("contact", "phone");

        form.Field("phone")!.Value.Should().Be("+351 912 345 678");
        form.Field("phone")!.Relevant.Should().BeTrue();
    }

    /// <summary>
    /// A condition that reads state the form does not own — a checkbox held by the page. Nothing
    /// inside the form changed, so nothing revalidated on its own... which is exactly what
    /// <see cref="FormController.Revalidate"/> is for.
    /// </summary>
    [Fact]
    public void AConditionOutsideTheFormIsAnnouncedByTheOwner()
    {
        var shipElsewhere = false;
        var form = new FormController();
        form.Add("address", rules: [Rules.When(() => shipElsewhere, Rules.Required())]);
        var beats = 0;
        form.Changed += () => beats++;

        form.Valid.Should().BeTrue();

        shipElsewhere = true;
        form.Revalidate();

        form.Valid.Should().BeFalse();
        beats.Should().Be(1, "the page told the form, and the form told the screen");
    }

    private static FormController Filled()
    {
        var form = SignUp();
        form.Set("email", "ana@equantic.tech");
        form.Set("password", "correcthorse");
        form.Set("confirm", "correcthorse");
        return form;
    }
}
