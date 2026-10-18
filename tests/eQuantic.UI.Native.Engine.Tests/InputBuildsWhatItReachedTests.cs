using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A frame after input builds what the input reached (#976): the positions its handlers marked, and
/// the stateful component that holds a field or a surface the host typed into, and no other
/// component. Every frame after input rebuilt every component while the host found its controls by
/// their layout path and could not say which component input had reached.
/// <para>
/// In the process dispatcher's collection, because one of them posts to it, and a host drawing in a
/// test beside it would run the work on its own thread.
/// </para>
/// </summary>
[Collection(ProcessDispatcherCollection.Name)]
public class InputBuildsWhatItReachedTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    /// <summary>A card whose button sets the card's own state, and which says when it builds.</summary>
    private sealed class Card(string name, List<string> builds, List<Card>? mounted = null) : StatefulComponent
    {
        private int _presses;

        public string Name { get; private set; } = name;

        public void Bump() => SetState(() => _presses++);

        public override void AdoptConfig(UiComponent next)
        {
            if (next is Card card) Name = card.Name;
        }

        protected override void OnMount() => mounted?.Add(this);

        public override VisualNode Build(ComponentContext context)
        {
            builds.Add(Name);
            return new Pressable(new Text($"{Name}: {_presses}", TypeRole.BodyM), Bump) { Label = Name };
        }
    }

    /// <summary>Three cards under a stateful board, and whatever is put beside them.</summary>
    private sealed class Board(List<string> builds, VisualNode? beside = null, List<Card>? mounted = null)
        : StatefulComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            builds.Add("board");
            var column = new Column(gap: Space.S2) { Width = SizeValue.Fill };
            if (beside is not null) column.Add(beside);
            for (var i = 0; i < 3; i++) column.Add(new Card($"card {i}", builds, mounted));
            return column;
        }
    }

    private static PhotonHost Open(VisualNode root)
    {
        var host = new PhotonHost(root, Theme, ThemeMode.Light, 500, 600);
        host.RenderFrame(new DisplayListBuilder());
        host.RenderFrame(new DisplayListBuilder());
        return host;
    }

    private static RealizeResult Frame(PhotonHost host) => host.RenderFrame(new DisplayListBuilder());

    /// <summary>Every text the frame drew, rich or plain.</summary>
    private static IEnumerable<string> Drawn(LayoutNode node)
    {
        if (node.Source is Text text) yield return text.PlainContent;
        foreach (var child in node.Children)
            foreach (var drawn in Drawn(child))
                yield return drawn;
    }

    [Fact]
    public void APress_BuildsThePressedCardAlone()
    {
        var builds = new List<string>();
        var host = Open(new Board(builds));
        builds.Clear();

        var button = host.LastFrame!.HitRegions.Single(region => region.Node.Label == "card 1").Bounds.Center;
        host.PressDown(button.X, button.Y);
        host.PressUp(button.X, button.Y);
        var frame = Frame(host);

        builds.Should().Equal(["card 1"], "the handler marked its own card, and a press reaches nothing else");
        Drawn(frame.Root).Should().Contain("card 1: 1");
    }

    /// <summary>A form whose field writes its value back with no SetState of its own: the host is
    /// what makes the form build, as a browser shows a key in an input whatever its handler does.</summary>
    private sealed class Form(List<string> builds) : StatefulComponent
    {
        private string _name = "";

        public override VisualNode Build(ComponentContext context)
        {
            builds.Add("form");
            return new TextEntry(_name, value => _name = value) { Label = "Name" };
        }
    }

    [Fact]
    public void AKeyTypedIntoAField_BuildsTheComponentThatHoldsIt()
    {
        var builds = new List<string>();
        var host = Open(new Board(builds, new Form(builds)));
        var field = host.LastFrame!.TextRegions.Single().Bounds.Center;
        host.PressDown(field.X, field.Y);
        host.PressUp(field.X, field.Y);
        Frame(host);
        builds.Clear();

        host.TextInput("a");
        Frame(host);
        host.TextInput("b");
        var frame = Frame(host);

        builds.Should().Equal(["form", "form"],
            "the field's value lives in the form, which builds after each key, and nothing else does");
        frame.TextRegions.Single().Entry.Value.Should().Be("ab",
            "each key starts from what the form made of the one before");
    }

    [Fact]
    public void AKeyTypedIntoACodeEditor_BuildsTheEditorAlone()
    {
        var builds = new List<string>();
        var editor = new CodeEditor("one needle two", "csharp") { ShowLineNumbers = false };
        var host = Open(new Board(builds, editor));
        var code = host.LastFrame!.CodeRegions.Single();
        var grid = code.Surface.Grid();
        host.PressDown(code.Bounds.X + grid.Origin.X + 1, code.Bounds.Y + grid.Origin.Y + 2);
        host.PressUp(code.Bounds.X + grid.Origin.X + 1, code.Bounds.Y + grid.Origin.Y + 2);
        Frame(host);
        builds.Clear();

        host.TextInput("x");
        var frame = Frame(host);

        builds.Should().BeEmpty("the editor holds its model and builds, and neither the board above it "
            + "nor a card beside it does");
        editor.Editor.Document.Text.Should().Be("xone needle two");
        Drawn(frame.Root).Should().Contain(text => text.Contains("xone"),
            "the frame drew the document the editor built with the key in it");
    }

    [Fact]
    public void WorkPostedForTheFrame_BuildsWhatItMarked()
    {
        var builds = new List<string>();
        var cards = new List<Card>();
        var host = Open(new Board(builds, mounted: cards));
        builds.Clear();

        PhotonDispatcher.Shared.Post(() => cards[2].Bump());
        host.NeedsRender.Should().BeTrue("posted work wakes the host for the frame it runs in");
        var frame = Frame(host);

        builds.Should().Equal(["card 2"], "the work marked one card, and waking the host marks nothing");
        Drawn(frame.Root).Should().Contain("card 2: 1");
    }

    [Fact]
    public void AnInvalidation_StillBuildsEveryComponent()
    {
        var builds = new List<string>();
        var host = Open(new Board(builds));
        builds.Clear();

        host.Invalidate();
        Frame(host);

        builds.Should().Equal(["board", "card 0", "card 1", "card 2"],
            "an invalidation changes what every build reads, so every component builds, ancestors first");
    }
}
