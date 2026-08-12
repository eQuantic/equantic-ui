using System.Globalization;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Hosting;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The culture hand (<see cref="PhotonCultureController"/>): the shell resolves the PLATFORM's
/// locale and Apply copies it onto BOTH .NET statics — <c>CurrentUICulture</c> picks resources,
/// <c>CurrentCulture</c> picks formats, the I18N-PLAN D13 pair — and the attached sink repaints
/// so a switch shows on the next frame. The same late-attach shape as the theme controller.
/// </summary>
public class CultureControllerTests
{
    /// <summary>Culture statics are process state — every test puts back what it found.</summary>
    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo? _defaultUi = CultureInfo.DefaultThreadCurrentUICulture;
        private readonly CultureInfo? _defaultFormat = CultureInfo.DefaultThreadCurrentCulture;
        private readonly CultureInfo _ui = CultureInfo.CurrentUICulture;
        private readonly CultureInfo _format = CultureInfo.CurrentCulture;

        public void Dispose()
        {
            CultureInfo.DefaultThreadCurrentUICulture = _defaultUi;
            CultureInfo.DefaultThreadCurrentCulture = _defaultFormat;
            CultureInfo.CurrentUICulture = _ui;
            CultureInfo.CurrentCulture = _format;
        }
    }

    [Fact]
    public void Apply_CopiesThePairOntoTheProcess_AndRepaints()
    {
        using var scope = new CultureScope();
        var host = new PhotonHost(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 10 }),
            PhotonTheme.Instance, ThemeMode.Light, 100, 50);
        var controller = new PhotonCultureController();
        controller.Attach(host.Invalidate);

        var builder = new DisplayListBuilder();
        host.RenderFrame(builder); // settle: the flip below must be the only dirt

        controller.Apply(CultureInfo.GetCultureInfo("pt-BR"), CultureInfo.GetCultureInfo("en-US"));

        CultureInfo.CurrentUICulture.Name.Should().Be("pt-BR", "UICulture picks resources");
        CultureInfo.CurrentCulture.Name.Should().Be("en-US", "Culture picks formats — the pair is independent");
        CultureInfo.DefaultThreadCurrentUICulture!.Name.Should().Be("pt-BR",
            "threads born later must agree with the user's choice");
        CultureInfo.DefaultThreadCurrentCulture!.Name.Should().Be("en-US");
        controller.UICulture.Name.Should().Be("pt-BR");
        controller.FormatCulture.Name.Should().Be("en-US");
        host.NeedsRender.Should().BeTrue("a culture switch must show on the next frame, not the next input");
    }

    [Fact]
    public void Apply_WithOnlyTheUiCulture_FormatsFollowIt()
    {
        using var scope = new CultureScope();
        var controller = new PhotonCultureController();

        controller.Apply(CultureInfo.GetCultureInfo("pt-BR"));

        CultureInfo.CurrentUICulture.Name.Should().Be("pt-BR");
        CultureInfo.CurrentCulture.Name.Should().Be("pt-BR",
            "null format means the UI culture's own formats — the single-locale platform case");
    }

    [Fact]
    public void ApplyBeforeAttach_SetsTheProcess_AndRepaintsNobody()
    {
        using var scope = new CultureScope();
        var controller = new PhotonCultureController();

        // Services exist before any window does — an early Apply must not need a host.
        controller.Apply(CultureInfo.GetCultureInfo("de-DE"));

        CultureInfo.CurrentUICulture.Name.Should().Be("de-DE");
        controller.UICulture.Name.Should().Be("de-DE");
    }
}
