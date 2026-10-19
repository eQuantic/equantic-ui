using System.Globalization;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Email.Tests;

/// <summary>
/// A message says which language it is in (#942). <c>&lt;html lang&gt;</c> was <c>en</c> on every
/// message, so screen readers and the clients that hyphenate or translate read a Portuguese message
/// as English, and an app sending in more than one language had nowhere to say so.
/// <para>
/// The culture is the message's, not only its attribute's: the tree is rendered under it, as a page
/// is rendered under its request's culture, so what its components write in Build (a number, a date,
/// a text looked up by culture) is in the same language the attribute names. The markup is not: a
/// length or a color reads the same in every culture, which the identical-markup case pins.
/// </para>
/// </summary>
public class MessageLanguageTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static readonly CultureInfo Portuguese = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>A component that writes a number and a date in Build, where the culture applies.</summary>
    private sealed class Figures : UiComponent
    {
        public override VisualNode Build(ComponentContext context) =>
            new Text($"{1.5}|{new DateTime(2024, 1, 15):d}", TypeRole.BodyM);
    }

    /// <summary>A component that builds itself forever: the render throws, naming it.</summary>
    private sealed class Endless : UiComponent
    {
        public override VisualNode Build(ComponentContext context) => new Endless();
    }

    private static Column Shape()
    {
        var actions = new Row(gap: Space.S2);
        actions.Add(new Text("Confirm", TypeRole.Label));
        actions.Add(new Text("Later", TypeRole.Label));

        var email = new Column(gap: Space.S4);
        email.Add(new Text("Welcome", TypeRole.Heading));
        email.Add(new Text("Your account is ready.", TypeRole.BodyM));
        email.Add(actions);
        email.Add(new Text("eQuantic", TypeRole.Caption));
        return email;
    }

    [Fact]
    public void AMessageNamesItsCultureOnTheDocument()
    {
        var message = EmailRenderer.Render(new Text("Olá", TypeRole.BodyM), Theme, culture: Portuguese);

        message.Html.Should().Contain("<html lang=\"pt-BR\" ");
    }

    [Fact]
    public void AMessageGivenNoCultureSaysEnglish()
    {
        var message = EmailRenderer.Render(new Text("Hello", TypeRole.BodyM), Theme);

        message.Html.Should().Contain("<html lang=\"en\" ");
    }

    [Fact]
    public void TheInvariantCultureSaysEnglish()
    {
        // .NET associates the invariant culture with the English language and no region, and its
        // name is empty, which is no language tag.
        var message = EmailRenderer.Render(new Text("Hello", TypeRole.BodyM), Theme, culture: CultureInfo.InvariantCulture);

        message.Html.Should().Contain("<html lang=\"en\" ");
    }

    [Theory]
    [InlineData("pt-BR", "1,5|15/01/2024")]
    [InlineData("en-US", "1.5|1/15/2024")]
    [InlineData("sv-SE", "1,5|2024-01-15")]
    public void TheTreeIsRenderedInTheMessagesCulture(string name, string written)
    {
        var message = EmailRenderer.Render(new Figures(), Theme, culture: CultureInfo.GetCultureInfo(name));

        message.Html.Should().Contain(written);
        message.PlainText.Should().Be(written);
    }

    [Fact]
    public void TheMarkupIsTheSameInEveryCulture()
    {
        // Only the attribute that names the language may differ: every length and color the
        // realizer writes is invariant, so a comma decimal or a culture's minus sign never reaches
        // a style.
        string Markup(string name) => EmailRenderer.Render(Shape(), Theme, preheader: "Ready", culture: CultureInfo.GetCultureInfo(name))
            .Html.Replace($"lang=\"{name}\"", "lang=\"\"");

        var english = Markup("en-US");
        foreach (var name in new[] { "pt-BR", "sv-SE", "de-DE", "ar-SA", "fa-IR" })
            Markup(name).Should().Be(english, $"{name} changes no markup");
    }

    [Fact]
    public void TheAmbientCultureIsRestoredAfterTheMessage()
    {
        var (formatting, lookup) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");

            EmailRenderer.Render(new Figures(), Theme, culture: Portuguese);
            CultureInfo.CurrentCulture.Name.Should().Be("de-DE");
            CultureInfo.CurrentUICulture.Name.Should().Be("fr-FR");

            // And when the render fails: the send fails, never the culture of what runs after it.
            var render = () => EmailRenderer.Render(new Endless(), Theme, culture: Portuguese);
            render.Should().Throw<InvalidOperationException>();
            CultureInfo.CurrentCulture.Name.Should().Be("de-DE");
            CultureInfo.CurrentUICulture.Name.Should().Be("fr-FR");
        }
        finally
        {
            CultureInfo.CurrentCulture = formatting;
            CultureInfo.CurrentUICulture = lookup;
        }
    }
}
