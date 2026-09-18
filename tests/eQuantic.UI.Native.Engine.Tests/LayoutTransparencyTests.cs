using System.Reflection;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// ONE statement of layout transparency, read by the three readers that need it (#225).
///
/// <para>
/// THE MEASUREMENT THAT SET THE SHAPE OF THESE TESTS. TWO readers in the layout engine kept
/// hand-written lists of wrappers to look through — a flex item's min-content floor and its
/// cross-axis size kind — and the lists differed in eight of the twenty wrappers. A third,
/// <c>Shrinkable</c>, had no wrapper arm at all. A fourth, the truncation contract, looked through
/// none of them: it found text with <c>children[i] is Text</c>, so a wrapped text was not a text as
/// far as it was concerned.
/// </para>
///
/// <para>
/// The issue framed the eight as omissions nobody had decided, and asked whether fixing them would
/// move pixels. Measured over the TWENTY wrappers that existed then — the list is twenty-one since
/// <see cref="Progress"/> joined, and these two numbers are a record of that measurement rather
/// than a running count — the answer was sharper than that in two ways, and both are pinned below.
/// ELEVEN of the twenty overflowed a fixed row outright — <c>Pressable</c>, <c>Link</c> and
/// <c>Hoverable</c> among them, which is most of the tappable text in a real screen — by 128dp
/// where the text was one unbreakable word and the bare one ellipsized. And the nine that did NOT
/// overflow agreed only in WIDTH: NINETEEN of the twenty wrapped to as many lines as they liked
/// where the bare text was cut to one — every wrapper but <c>Overlay</c>, which takes no space in
/// the flow at all. The one test that guarded this
/// (<c>ALayoutTransparentWrapper_DoesNotChangeItsChildsFloor</c>) was measuring the one number on
/// which the two agreed.
/// </para>
///
/// <para>
/// So these enumerate BY REFLECTION rather than listing the eight: every wrapper the vocabulary has
/// is asserted against the bare child, and a twenty-second is covered on the day it is declared.
/// </para>
/// </summary>
public class LayoutTransparencyTests
{
    private static readonly LayoutContext Ctx =
        new(PhotonTheme.Instance, ApproximateTextMeasurer.Instance);

    private static Box FixedBox(float w, float h) => new(new BoxStyle { Width = w, Height = h });

    /// <summary>
    /// Every wrapper in the vocabulary, each around the child it is handed. Hand-written because
    /// their constructors take different arguments, and pinned COMPLETE by
    /// <see cref="TheVocabularysWrappers_AreAllCoveredHere"/> — so the list cannot fall behind the
    /// vocabulary the way the three it replaces did.
    /// </summary>
    private static IReadOnlyList<SingleChildNode> Wrappers(VisualNode c) =>
    [
        new Adjustable(c, _ => { }),
        new Progress(c),
        new CodeSurface(c, new CodeEditorController()),
        new DragDismiss(c),
        new Draggable(c),
        new Flexible(c),
        new Hoverable(c, _ => { }),
        new InFlow(c),
        new InView(c, _ => { }),
        new Link("#", c),
        new LoopMotion(c, LoopEffect.SlideX, 0, 1, 100),
        new Overlay(c),
        new Pinned(c),
        new Positioned(c, top: 0),
        new Presence(c),
        new Pressable(c),
        new SafeArea(c),
        new ScrollView(c),
        new SheetSurface(c, new SheetController()),
        new Shortcut(c, new KeyChord("a"), () => { }),
        new Simulated(new SimulatedState(), c),
    ];

    private static IEnumerable<SingleChildNode> Transparent(VisualNode c) =>
        Wrappers(c).Where(w => w.IsLayoutTransparent());

    [Fact]
    public void TheVocabularysWrappers_AreAllCoveredHere()
    {
        var declared = typeof(SingleChildNode).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(SingleChildNode)) && !t.IsAbstract)
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Wrappers(new Text("x")).Select(w => w.GetType().Name).OrderBy(n => n, StringComparer.Ordinal)
            .Should().Equal(declared,
                "a wrapper the vocabulary declares and this file does not build is a wrapper nothing "
                + "below asserts anything about — which is exactly how the two hand-kept lists "
                + "drifted eight places apart");
    }

    /// <summary>
    /// The statement itself. Three wrappers are not transparent, and each is named with what it
    /// establishes that its child cannot speak for. Pinned as a SET so neither side can grow
    /// quietly: a fourth exception has to be argued for here.
    /// </summary>
    [Fact]
    public void ExactlyThreeWrappers_CarryGeometryOfTheirOwn()
    {
        Wrappers(new Text("x")).Where(w => !w.IsLayoutTransparent()).Select(w => w.GetType().Name)
            .Should().BeEquivalentTo(["ScrollView", "Overlay", "Positioned"],
                "a scroller has its own viewport, an Overlay takes no space in the flow, and a "
                + "Positioned is a contract with a Stack");
    }

    /// <summary>
    /// A transparent wrapper does NOT stretch a child that states its own size — in a column that
    /// fills, under either cross alignment.
    /// <para>
    /// This is the native half of the web's role-host rule (<c>RoleBearingHostBoundsTests</c>), and
    /// it is here because the web fix was challenged as the thing that CREATED a divergence: the
    /// claim was that native passes the incoming stretch through the wrapper, so a hugging Box
    /// inside a filling Column resolves to the column's width on Photon while the web host hugs.
    /// Measured, it does not — a Box with a stated width is a fixed size, and stretch does not
    /// overrule one. The two targets agree, which is what the web fix bought; before it the web was
    /// the one that spanned.
    /// </para>
    /// <para>
    /// Swept over every transparent wrapper rather than the two that carry a role, because the rule
    /// is the shape's and not the role's — the role is only what makes the divergence audible.
    /// </para>
    /// </summary>
    [Fact]
    public void ATransparentWrapper_DoesNotStretchAChildThatStatesItsOwnSize()
    {
        var stretched = new List<string>();

        foreach (var cross in new[] { CrossAlign.Start, CrossAlign.Stretch })
        foreach (var wrapper in Transparent(FixedBox(120, 8)))
        {
            var column = new Column(gap: 0) { Width = SizeValue.Fill, Cross = cross };
            column.Add(wrapper);

            var laid = LayoutEngine.Layout(column, 600f, 400f, Ctx);
            var host = laid.Children[0];

            if (host.Bounds.Width != 120)
                stretched.Add($"{wrapper.GetType().Name} ({cross}) = {host.Bounds.Width}");
        }

        string.Join(", ", stretched).Should().BeEmpty(
            "the column is 600 wide and the child said 120, so a wrapper that came back 600 took a "
            + "size its child never asked for — and the web host, which hugs, would then be drawing "
            + "and announcing a different box from the one Photon lays out");
    }

    // ---- reader 1 + reader 4: the floor, and the contract that cuts the text -------------------

    /// <summary>
    /// A row fixed at 150 with a 120 box in it leaves 22dp. What lands in those 22dp must not depend
    /// on whether the text was wrapped — in WIDTH, in LINE COUNT, and in whether the line carries
    /// the mark. The last two are what the old guard could not see.
    /// </summary>
    private static (float Width, float Right, int Lines, bool Ellipsized) InTheSlot(VisualNode second)
    {
        var row = new Row(gap: 8) { Width = SizeValue.Fixed(150) };
        row.Add(FixedBox(120, 20));
        row.Add(second);
        var item = LayoutEngine.Layout(row, 150, 300, Ctx).Children[1];
        var text = item.Text ?? (item.Children.Count > 0 ? item.Children[0].Text : null);
        return (item.Bounds.Width, item.Bounds.X + item.Bounds.Width,
            text?.Lines.Count ?? -1, text?.Lines[^1].Ellipsized ?? false);
    }

    [Theory]
    [InlineData("antidisestablishmentarianism")]      // ONE word: nothing can wrap, only cut
    [InlineData("a very long description that will not fit")]  // MANY: wrapping is the alternative
    public void ATransparentWrapper_IsCutExactlyAsTheBareTextIs(string content)
    {
        var bare = InTheSlot(new Text(content, TypeRole.BodyM));

        foreach (var wrapper in Transparent(new Text(content, TypeRole.BodyM)))
        {
            // A Flexible is sized from the row's LEFTOVER rather than from its content, so it takes
            // the slot it is granted; that is the one transparency does not decide.
            if (wrapper is Flexible) continue;

            var wrapped = InTheSlot(wrapper);
            var why = $"{wrapper.GetType().Name} carries no geometry of its own";

            wrapped.Right.Should().BeLessThanOrEqualTo(150.01f,
                $"{why}, and eleven of the twenty used to run past the end of this row");
            wrapped.Width.Should().BeApproximately(bare.Width, 0.01f, why);
            wrapped.Lines.Should().Be(bare.Lines,
                $"{why} — and WIDTH parity alone hid that a wrapped text wrapped where the bare one "
                + "was cut, because a single unbreakable word cannot wrap either way");
            wrapped.Ellipsized.Should().Be(bare.Ellipsized, why);
        }
    }

    [Fact]
    public void ARichText_KeepsItsRuns_WhenTheContractCutsIt()
    {
        // The cut used to be built by hand from `ctx.Measurer`, on a node rebuilt from PlainContent:
        // a paragraph of runs went in and a flat string came out, losing every run's colour, face
        // and link destination. Now it re-measures through the same pass, so the runs survive it.
        var rich = new Text("placeholder")
        {
            Spans = [new TextRun("a very long "), new TextRun("emphasised"),
                     new TextRun(" description that cannot possibly fit")],
        };

        var alone = LayoutEngine.Layout(rich, 400, 300, Ctx);
        alone.TextRuns.Should().NotBeNullOrEmpty("a rich paragraph is laid out as runs");

        var row = new Row(gap: 8) { Width = SizeValue.Fixed(150) };
        row.Add(FixedBox(120, 20));
        row.Add(rich);
        var cut = LayoutEngine.Layout(row, 150, 300, Ctx).Children[1];

        cut.TextRuns.Should().NotBeNullOrEmpty("the cut must not flatten the paragraph");
        cut.Text!.Lines.Should().HaveCount(1, "spec A2 cuts it to a line rather than letting it wrap");
        cut.Text.Lines[^1].Ellipsized.Should().BeTrue();
        cut.TextRuns![^1].Content.Should().Be("…",
            "the mark is INSIDE the measurement (ITextMeasurer's contract), so the fragments a "
            + "rasterizer draws carry it and nothing downstream adds one outside the promised width");
        (cut.TextRuns[^1].X + cut.TextRuns[^1].Width).Should().BeLessThanOrEqualTo(cut.Bounds.Width + 0.01f,
            "and it is inside the width the measurement reports");
    }

    /// <summary>
    /// A CUT LINE NEVER REPORTS MORE ROOM THAN IT WAS GIVEN, even when there is nothing left to
    /// drop. Copilot's second review on #232 named this, and the mechanism it gave was not the one
    /// measured — worth recording, because the consequence was right anyway.
    ///
    /// <para>
    /// It read as "the ellipsis fragment is placed outside the measured width". It is not: the
    /// measured width GROWS to include the mark, so the fragment is inside it. What the growth
    /// breaks is the other side — the number handed back to layout exceeded the room the parent
    /// offered, and a node that reports more room than it was given makes its parent grow. The
    /// plain path never did that: it has always ended a cut line with
    /// <c>Min(lineWidth + ellipsis, maxWidth)</c>.
    /// </para>
    ///
    /// <para>
    /// So the two paths are pinned AGAINST EACH OTHER rather than against a number. An unbreakable
    /// word wider than its box overflows on both and the realizer clips it; what must not differ is
    /// what they report. <see cref="RichAndPlainTextReportTheSameRoom"/> is the general case — the
    /// divergence turned out not to be about the cut line at all.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(40f)]
    [InlineData(48f)]
    [InlineData(60f)]
    public void ACutLineReportsAtMostTheRoomItHad_OnBothTextPaths(float room)
    {
        const string content = "alphabet zzzzzzzzzzzzzzzz";

        static float WidthIn(VisualNode text, float room) =>
            LayoutEngine.Layout(new Box(new BoxStyle { Width = SizeValue.Fixed(room) }, text),
                400, 300, Ctx).Children[0].Bounds.Width;

        // One word fills the line and the next overflows, so the drop loop has nothing to take.
        var rich = new Text("placeholder")
        {
            Spans = [new TextRun("alphabet"), new TextRun(" zzzzzzzzzzzzzzzz")],
            MaxLines = 1,
        };
        var plain = new Text(content, TypeRole.BodyL) { MaxLines = 1 };

        WidthIn(plain, room).Should().BeLessThanOrEqualTo(room + 0.01f,
            "the plain path has always clamped a cut line to the room it had");
        WidthIn(rich, room).Should().BeLessThanOrEqualTo(room + 0.01f,
            "and the runs path is the same measurement with per-word rectangles, not a different "
            + "contract — it reported 75.48 into a box of " + room + " before this");

        // The ITextMeasurer contract, on the path that has rectangles to check it with: a cut line
        // is measured WITH its mark, so nothing it lays out may sit outside the width it reports.
        var laid = LayoutEngine.Layout(new Box(new BoxStyle { Width = SizeValue.Fixed(room) }, rich),
            400, 300, Ctx).Children[0];
        laid.TextRuns.Should().NotBeNullOrEmpty();
        laid.TextRuns!.Should().Contain(f => f.Content == "\u2026", "the line was cut");
        foreach (var fragment in laid.TextRuns!)
            (fragment.X + fragment.Width).Should().BeLessThanOrEqualTo(laid.Bounds.Width + 0.01f,
                $"'{fragment.Content}' is drawn where the measurement put it, and the realizer clips "
                + "at the bounds — the mark used to land entirely outside them, present in the "
                + "fragments and invisible on the screen");

        // The cut keeps the LONGEST prefix that leaves the mark its width — not merely a fitting
        // one. The search for it is binary, so settling for a shorter cut is the way it can regress
        // and stay green on every assertion above.
        var kept = laid.TextRuns!.First().Content;
        var mark = laid.TextRuns!.Single(f => f.Content == "\u2026");
        kept.Should().Be("alphabet"[..kept.Length], "the cut is a prefix of the word");
        kept.Length.Should().BeLessThan("alphabet".Length, "and a real one at this width");

        float Measure(string t) => Ctx.Measurer
            .Measure(t, new Text(t, TypeRole.BodyL).Resolve(Ctx.Theme), Ctx.TypeScale,
                float.PositiveInfinity, 1).Width;

        (Measure("alphabet"[..(kept.Length + 1)]) + mark.Width).Should().BeGreaterThan(room + 0.01f,
            "one character more would not have left the mark its room — which is what makes the "
            + "kept prefix the longest rather than just a fitting one");
    }

    /// <summary>
    /// THE TWO TEXT PATHS REPORT THE SAME ROOM, at every width including zero — the general form of
    /// the case above, and the one that showed the first fix had been too narrow.
    ///
    /// <para>
    /// A third review round said <c>MeasureRuns</c> read <c>maxW &lt;= 0</c> as unbounded, so a rich
    /// paragraph laid out at full width in a zero-width slot. Measured, that was real and it was not
    /// the whole of it: the runs path clamped NO line to its limit, where the plain measurer has
    /// always committed each one at <c>Min(candidate, maxWidth)</c>. A rich paragraph reported 170dp
    /// into a box of 0, and 54.4 into a box of 10 — the second has nothing to do with zero.
    /// </para>
    ///
    /// <para>
    /// A zero-width slot is not hypothetical: the flex pass hands one down whenever a row has
    /// nothing left to give, which is the same truncation path the rest of this file is about.
    /// </para>
    ///
    /// <para>
    /// THE WIDTHS HERE ARE THE ONES WHERE THE ROOM BINDS, and the reason is a SEPARATE defect this
    /// test found and does not fix — see
    /// <see cref="ARichParagraphMeasuresNarrowerThanItsPlainTwin_BecauseItsSpacesCostNothing"/>.
    /// Given room to spare the two disagree for a reason that has nothing to do with clamping, so
    /// asserting parity there would be asserting two things and blaming this one.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    [InlineData(10f)]
    [InlineData(40f)]
    public void RichAndPlainTextReportTheSameRoom(float room)
    {
        const string content = "alpha beta gamma delta";

        static float WidthIn(VisualNode text, float room) =>
            LayoutEngine.Layout(new Box(new BoxStyle { Width = SizeValue.Fixed(room) }, text),
                400, 300, Ctx).Children[0].Bounds.Width;

        var rich = new Text("placeholder")
        {
            Spans = [new TextRun("alpha beta"), new TextRun(" gamma delta")],
        };
        rich.PlainContent.Should().Be(content,
            "the two have to be the same paragraph for the comparison to mean anything");

        WidthIn(rich, room).Should().BeApproximately(WidthIn(new Text(content, TypeRole.BodyL), room),
            0.01f, "runs are how a paragraph is measured when it has emphasis in it, not a different "
            + "contract for how much room it may claim");
    }

    /// <summary>
    /// A DEFECT THIS PR FOUND AND DELIBERATELY DOES NOT FIX, pinned so it cannot change unnoticed
    /// and so the number is on the record.
    ///
    /// <para>
    /// Every inter-word space in a RICH paragraph measures zero. <c>MeasureRuns</c> asks the
    /// measurer for each piece including the spaces, and <c>ApproximateTextMeasurer</c> splits its
    /// input on <c>' '</c> with <c>RemoveEmptyEntries</c> — so a lone space is an empty word list
    /// and comes back 0 wide. The plain path never asks: it adds <c>Advance(' ')</c> between words
    /// itself.
    /// </para>
    ///
    /// <para>
    /// Measured: identical for one word, and exactly 5.1dp short per GAP after that. A paragraph
    /// with emphasis in it therefore claims less room than the same sentence without, which is a
    /// different mechanism from the clamp this PR is about — it lives in the measurer, and changing
    /// it moves every rich paragraph's geometry. Left for its own change rather than widened into
    /// this one.
    /// </para>
    /// </summary>
    [Fact]
    public void ARichParagraphMeasuresNarrowerThanItsPlainTwin_BecauseItsSpacesCostNothing()
    {
        static float Unbounded(VisualNode t) => LayoutEngine.Layout(t, 4000, 300, Ctx).Bounds.Width;

        static (float Rich, float Plain) Pair(string content) =>
            (Unbounded(new Text("x") { Spans = [new TextRun(content)] }),
             Unbounded(new Text(content, TypeRole.BodyL)));

        var one = Pair("alpha");
        var two = Pair("alpha beta");
        var four = Pair("alpha beta gamma delta");

        one.Rich.Should().BeApproximately(one.Plain, 0.01f,
            "with no space in it there is nothing to lose, which is what says the loss is the spaces");

        var perGap = two.Plain - two.Rich;
        perGap.Should().BeGreaterThan(0, "a space costs nothing on the runs path and something on the other");
        (four.Plain - four.Rich).Should().BeApproximately(perGap * 3, 0.01f,
            "and it is exactly one space's width per gap — three gaps, three times the shortfall");
    }

    /// <summary>
    /// THE MARK BELONGS TO THE TEXT IT TERMINATES. Copilot found this on #232 and it was real: the
    /// ellipsis took its face from <c>runStyle</c> at the moment of the wrap decision — the style of
    /// the word that FAILED to fit, which is the first word of the next run as often as not — and
    /// carried neither the ink nor the link of the run it was actually ending.
    ///
    /// <para>
    /// Measured, the link half is the one that bites: a truncated link's ellipsis had no
    /// <c>Destination</c>, so on a target that hit-tests per fragment the end of the link was not
    /// pressable. And the review's own remedy needed one more step than it said — the last fragment
    /// on a cut line is frequently the SPACE that follows the last word, which already belongs to
    /// the next run, so "the final fragment that remains" still lands on the wrong face. It is the
    /// last VISIBLE one.
    /// </para>
    /// </summary>
    [Fact]
    public void TheMarkTakesTheFaceAndTheLinkOfTheRunItEnds()
    {
        // Run 1 fits whole and is a mono link. Run 2's first word is what overflows, so the style
        // in hand at the wrap decision is run 2's — and the space between them is run 2's too.
        var rich = new Text("placeholder")
        {
            Spans =
            [
                new TextRun("alpha", Mono: true) { Destination = "https://example.test" },
                new TextRun(" deltaepsilonzetaeta"),
            ],
        };

        var row = new Row(gap: 8) { Width = SizeValue.Fixed(190) };
        row.Add(FixedBox(120, 20));
        row.Add(rich);
        var cut = LayoutEngine.Layout(row, 190, 300, Ctx).Children[1];

        var mark = cut.TextRuns!.Single(f => f.Content == "\u2026");
        var word = cut.TextRuns!.First(f => f.Content == "alpha");

        mark.Style.Mono.Should().BeTrue(
            "the mark ends the mono run, not the proportional one whose word could not fit");
        mark.Destination.Should().Be(word.Destination,
            "an ellipsis is the tail of the link it cut, and a fragment without a destination is "
            + "not pressable where a target hit-tests per fragment");
        mark.Color.Should().Be(word.Color);
    }

    // ---- reader 2: the cross-axis size kind ----------------------------------------------------

    /// <summary>
    /// A row 100 tall that stretches its children, holding one 40×20 box. Stretch fills AUTO cross
    /// sizes only, so the box keeps its 20 — and so must a wrapper around it. Three of the twenty
    /// answered Hug here and were stretched to 100, overriding the height the author pinned.
    /// </summary>
    [Fact]
    public void ATransparentWrapper_TakesItsChildsCrossSizeKind()
    {
        static float HeightUnderStretch(VisualNode only)
        {
            var row = new Row(gap: 0, cross: CrossAlign.Stretch) { Height = SizeValue.Fixed(100) };
            row.Add(only);
            return LayoutEngine.Layout(row, 400, 100, Ctx).Children[0].Bounds.Height;
        }

        HeightUnderStretch(FixedBox(40, 20)).Should().Be(20, "an explicit cross size is kept");

        foreach (var wrapper in Transparent(FixedBox(40, 20)))
            HeightUnderStretch(wrapper).Should().Be(20,
                $"{wrapper.GetType().Name} wraps a box whose height the author fixed, and a wrapper "
                + "that answered Hug here stretched it to the line");
    }

    // ---- the third reader, and the one that turned out not to need the statement ---------------

    private static float WidthOfSecond(VisualNode second, float rowWidth = 150)
    {
        var row = new Row(gap: 8) { Width = SizeValue.Fixed(rowWidth) };
        row.Add(FixedBox(120, 20));
        row.Add(second);
        return LayoutEngine.Layout(row, rowWidth, 300, Ctx).Children[1].Bounds.Width;
    }

    /// <summary>
    /// The FLOOR a wrapper reports is its child's, so a shrinking row squeezes the space between two
    /// buttons and never the word inside one.
    ///
    /// <para>
    /// A fixed size is the wrong probe for this and a first draft used one: a Box measured at a
    /// narrower bound comes back at its fixed width regardless, so the assertion held with the
    /// transparency removed. What actually responds to the bound is content that can be squeezed —
    /// a padded box around a word — which is why this builds the shape the shared-buttons golden
    /// builds. That golden is what caught the draft.
    /// </para>
    /// </summary>
    [Fact]
    public void ATransparentWrapper_FloorsWhereItsChildFloors()
    {
        // A "button": a padded box around a word. Its floor is the word plus the padding.
        static Box Labelled(string word) =>
            new(new BoxStyle { Padding = EdgeInsets.All(8), Height = 20 },
                new Text(word, TypeRole.BodyM));

        // Narrow enough that the row must take width from somewhere.
        var bare = WidthOfSecond(Labelled("Continue"), rowWidth: 190);
        bare.Should().BeGreaterThan(0);

        foreach (var wrapper in Transparent(Labelled("Continue")))
        {
            if (wrapper is Flexible) continue;

            WidthOfSecond(wrapper, rowWidth: 190).Should().BeApproximately(bare, 0.01f,
                $"{wrapper.GetType().Name} reports the floor of what it wraps, so the row cannot "
                + "squeeze it below the word it contains — with the floor answering zero instead, "
                + "the wrapped button collapses and the bare one does not");
        }
    }

    /// <summary>
    /// THE CASE THAT SAYS <c>Shrinkable</c> MUST NOT LOOK THROUGH, and the reason the statement has
    /// three readers instead of the four #225 expected.
    ///
    /// <para>
    /// <c>Shrinkable</c> excludes a <see cref="Flexible"/> because its size came from the row's
    /// LEFTOVER — a contract with the direct parent, exactly like a <see cref="Positioned"/>'s with
    /// a <see cref="Stack"/>. A wrapper is not that parent and was granted nothing, so an arm that
    /// inherited the exclusion inherited a promise nobody made: measured, the wrapped Flexible then
    /// kept the whole row (100/150/220) beside a 120 sibling, while the bare one yields 0/22/92.
    /// </para>
    ///
    /// <para>
    /// Pinned as PARITY with the bare Flexible, because the numbers are the flex pass's business
    /// and the claim is only that wrapping changes nothing.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(100f)]
    [InlineData(150f)]
    [InlineData(220f)]
    public void AWrappedFlexible_YieldsExactlyAsABareOneDoes(float rowWidth)
    {
        static Flexible Fill() =>
            new(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 20 }));

        var bare = WidthOfSecond(Fill(), rowWidth);

        WidthOfSecond(new Pressable(Fill()), rowWidth).Should().BeApproximately(bare, 0.01f,
            "a flex weight is a contract with the ROW, and the row granted the wrapper nothing");
        WidthOfSecond(new SafeArea(Fill()), rowWidth).Should().BeApproximately(bare, 0.01f);
    }

    // ---- the flag the cut travels on -----------------------------------------------------------

    [Fact]
    public void TheCut_DoesNotEscapeIntoANestedContainer()
    {
        // `Truncating` rides the constraints through transparent wrappers, which is how it reaches a
        // wrapped text. Every other door measures its children through `ForChild`, which clears it —
        // so a Row nested inside a cut wrapper wraps its own text as it always did. Without that,
        // one overflowing item would ellipsize paragraphs anywhere below it.
        var inner = new Text("a very long description that will not fit", TypeRole.BodyM);
        var row = new Row(gap: 8) { Width = SizeValue.Fixed(150) };
        row.Add(FixedBox(120, 20));
        row.Add(new Pressable(new Column(gap: 0) { inner }));

        var column = LayoutEngine.Layout(row, 150, 300, Ctx).Children[1].Children[0];

        column.Children[0].Text!.Lines.Count.Should().BeGreaterThan(1,
            "the text is in a Column, not directly under the wrapper the row is cutting");
    }
}
