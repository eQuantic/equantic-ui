using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;
using static eQuantic.UI.Components.UI;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The six nodes that used to be reachable only with <c>new</c> (#251), each asserted where the
/// factory contract is actually felt: a factory and the constructor it mirrors must build the SAME
/// node, so that "named arguments carry over unchanged" is a fact rather than a promise.
///
/// <para>
/// <see cref="UiFactoryConformanceTests"/> already checks the SHAPE of every factory by reflection
/// — names, types, defaults, the init-only tail. What it cannot see is whether the body applies
/// the tail it declares, or whether a tail parameter's default agrees with the property's own.
/// Those are value questions, and this file asks them for the six.
/// </para>
/// </summary>
public class NoNodeNeedsNewTests
{
    private static readonly VisualNode Child = new Text("x", TypeRole.BodyM);

    [Fact]
    public void CameraPreview_CarriesItsRadiusAndItsName()
    {
        var made = CameraPreview(null, 320, 240, new CornerRadii(Radius.Md), "Front camera");

        made.Session.Should().BeNull("a null session is the placeholder state, not a missing argument");
        made.Width.Should().Be(320);
        made.Height.Should().Be(240);
        made.CornerRadius.Should().Be(new CornerRadii(Radius.Md));
        made.Label.Should().Be("Front camera");
    }

    [Fact]
    public void LoopMotion_MirrorsAllFivePositionalsAndTheRestPolicy()
    {
        var made = LoopMotion(Child, LoopEffect.SlideX, -0.35f, 1.35f, 1400, hideAtRest: true);

        made.Effect.Should().Be(LoopEffect.SlideX);
        made.FromX.Should().Be(-0.35f);
        made.ToX.Should().Be(1.35f);
        made.DurationMs.Should().Be(1400);
        made.HideAtRest.Should().BeTrue();
    }

    [Fact]
    public void CodeSurface_KeepsTheGeometryDefaultsTheConstructorGives()
    {
        var editor = new CodeEditorController();
        var changed = 0;
        var made = CodeSurface(Child, editor, onChanged: () => changed++, label: "Source", autofocus: true);

        made.Editor.Should().BeSameAs(editor, "the controller is a live object the caller owns");
        made.Label.Should().Be("Source");
        made.Autofocus.Should().BeTrue();

        made.OnChanged!.Invoke();
        changed.Should().Be(1);

        // The geometry is deliberately NOT in the tail, so the factory must leave the node's own
        // defaults exactly where `new` leaves them.
        var bare = new CodeSurface(Child, editor);
        made.LineHeight.Should().Be(bare.LineHeight);
        made.ColumnWidth.Should().Be(bare.ColumnWidth);
        made.ContentTop.Should().Be(bare.ContentTop);
        made.ContentLeft.Should().Be(bare.ContentLeft);
    }

    [Fact]
    public void SheetSurface_CarriesTheVirtualizedWindowOrigin()
    {
        var controller = new SheetController();
        var made = SheetSurface(Child, controller, label: "Budget", firstRow: 40, firstCol: 3);

        made.Controller.Should().BeSameAs(controller);
        made.Label.Should().Be("Budget");
        made.FirstRow.Should().Be(40);
        made.FirstCol.Should().Be(3);

        var bare = new SheetSurface(Child, controller);
        made.HeaderWidth.Should().Be(bare.HeaderWidth);
        made.HeaderHeight.Should().Be(bare.HeaderHeight);
    }

    /// <summary>
    /// The rows are LAST, like every container's children — and that is the whole of what the
    /// constructor change bought, so it is asserted by building the same grid both ways.
    /// </summary>
    [Fact]
    public void Navigable_TakesItsRowsLastAndAgreesWithNew()
    {
        var moves = new List<NavigableMove>();
        void Move(NavigableMove m) => moves.Add(m);
        VisualNode[] rows = [new Text("S", TypeRole.BodyM), new Text("1", TypeRole.BodyM)];

        var made = Navigable(Move, rows, label: "July 2026", hasHeaderRow: true, activeCell: (1, 0));
        var written = new Navigable(Move, rows)
        {
            Label = "July 2026",
            HasHeaderRow = true,
            ActiveCell = (1, 0),
        };

        made.Rows.Should().Equal(written.Rows);
        made.Label.Should().Be(written.Label);
        made.HasHeaderRow.Should().Be(written.HasHeaderRow);
        made.ActiveCell.Should().Be(written.ActiveCell);
        made.Role.Should().Be(written.Role);

        made.OnMove(NavigableMove.NextItem);
        moves.Should().ContainSingle().Which.Should().Be(NavigableMove.NextItem);
    }

    /// <summary>
    /// THE TRAP THIS FILE EXISTS FOR. <see cref="WebFrame.Width"/> and <see cref="WebFrame.Height"/>
    /// default to <see cref="SizeValue.Fill"/> on the node, and C# has no constant expression for
    /// that — so a <c>SizeValue width = default</c> tail parameter would compile, satisfy every
    /// reflection rule in <see cref="UiFactoryConformanceTests"/>, and silently hand back a HUG
    /// frame from the factory where <c>new</c> gives a filling one.
    /// <para>
    /// The factory therefore carries no tail for them, and this is what says so. Mutation: add
    /// <c>SizeValue width = default, SizeValue height = default</c> to <c>UI.WebFrame</c> and apply
    /// them, and this fails while nothing else does.
    /// </para>
    /// </summary>
    [Fact]
    public void WebFrame_FillsLikeNewDoes_BecauseItsSizeIsNotInTheTail()
    {
        var content = WebContent.Url("https://example.com/embed");

        var made = WebFrame(content, "Embed");
        var written = new WebFrame(content, "Embed");

        made.Width.Should().Be(written.Width).And.Be(SizeValue.Fill);
        made.Height.Should().Be(written.Height).And.Be(SizeValue.Fill);
        made.Sandbox.Should().Be(written.Sandbox).And.Be(WebSandbox.Scripts);
        made.Content.Should().Be(content);
        made.Title.Should().Be("Embed");
    }

    [Fact]
    public void WebFrame_GrantsEachCapabilityByName()
    {
        var made = WebFrame(WebContent.Document("<p>a</p>"), "Preview",
            sandbox: WebSandbox.Scripts | WebSandbox.Forms,
            cornerRadius: new CornerRadii(Radius.Md));

        made.Content.IsInline.Should().BeTrue();
        made.Sandbox.Should().HaveFlag(WebSandbox.Forms);
        made.Sandbox.Should().NotHaveFlag(WebSandbox.SameOrigin);
        made.CornerRadius.Should().Be(new CornerRadii(Radius.Md));
    }
}
