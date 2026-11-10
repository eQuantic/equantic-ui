using eQuantic.UI.Primitives;
using Xunit;
using static eQuantic.UI.Components.UI;

namespace eQuantic.UI.Native.Testing.Tests;

/// <summary>
/// THE EXAMPLE the wiki's Testing page shows for Photon, written here exactly as it shows it, beside
/// the card it measures, which is the page's own stateless card. The page is copied from this test,
/// never the other way round, so what an app author copies is code a build has run.
/// </summary>
public class DocumentedMeasurementTests
{
    public sealed class ProfileCard(string name, string role) : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => Card(Column(gap: Space.S2, children:
        [
            Text(name, TypeRole.Title, headingLevel: 2),
            Text(role, TypeRole.BodyM),
        ]));
    }

    [Theory]
    [InlineData(1280f, 1)]
    [InlineData(360f, 2)]
    public void ACardOnPhoton(float width, int lines)
    {
        using var tester = new PhotonTester(width: width, height: 640);

        tester.Pump(new ProfileCard("Ana Lima", "Principal Engineer, Platform Reliability and Developer Experience"));

        var role = tester.Find("Principal Engineer, Platform Reliability and Developer Experience");
        Assert.Equal(lines, role.Text!.Lines.Count);
        Assert.True(role.Bounds.Right <= tester.Root.Bounds.Right);
    }
}
