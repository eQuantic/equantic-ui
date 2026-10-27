using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// WHAT CHANGED SINCE LAST FRAME, and nothing else. The piece #247 says is missing: the semantics
/// tree is a snapshot, so announcing means remembering what a region said and noticing that it no
/// longer says it.
///
/// <para>
/// Frame-driven, not query-driven. <c>PhotonHost.Semantics()</c> is asked by a bridge whenever
/// assistive tech is curious — never, once, or three times in a frame — so a diff hung off that
/// would announce a number of times nobody chose. This runs once, from <c>RenderFrame</c>, which is
/// the only clock that ticks exactly as often as the content can change.
/// </para>
///
/// <para>
/// WHAT IT COSTS WHEN NOTHING IS LIVE, which is almost every frame of almost every app: a loop over
/// no marks. That matters because <c>SteadyMotion_WithRecycledFrames_AllocatesFarLess</c> charges a
/// frame by the byte, and an accessibility feature that taxed every animation would be paid for by
/// users who never turn a reader on.
/// </para>
///
/// <para>
/// ARRIVAL COUNTS, after the first frame. A Banner or a Toast that appears IS the event — those two
/// are why `LiveRegion` exists — so a region seen for the first time announces. The one exception
/// is the host's FIRST frame, where every region is new by definition and announcing would make a
/// page read its own status line aloud on load.
/// </para>
///
/// <para>
/// THE DEBOUNCE IS NOT THE URGENCY. Assertive says how hard an announcement interrupts; it says
/// nothing about how often one may be made, and a region whose text changes every frame — a
/// percentage, a clock — would otherwise produce sixty announcements a second in either urgency.
/// So a region announces at most once per <see cref="QuietMs"/>, and what it announces when the
/// window opens is its CURRENT text rather than the one that was pending: a reader saying "41%"
/// when the bar reads 58% is worse than saying nothing. A change inside the window is OWED, not
/// said: until #1081 it was recorded as said and dropped, so the second of two arrows a second apart
/// in a spreadsheet was never heard. While one is owed the host keeps asking for frames
/// (<see cref="Pending"/>), so the window's end is a frame even when nothing else moves; and a
/// region back to what it last said owes nothing.
/// </para>
///
/// <para>
/// WHAT A REGION SAID, and when it last spoke, live on the region's position
/// (<see cref="LiveRegionState"/>), so they leave the tree with it: a toast that goes and comes back
/// with the same words is a new region, and the user was not there for the first one. They were
/// kept by path, and a region that left had to be forgotten by hand every frame, or a list whose rows
/// are toasts grew a row per path for the life of the app.
/// </para>
/// </summary>
internal sealed class LiveRegionAnnouncer
{
    /// <summary>
    /// How long a region stays quiet after speaking. One second is the reading rate the platforms
    /// assume — VoiceOver and TalkBack both take longer than that to read a short sentence, so a
    /// shorter window would queue announcements faster than any of them can voice them, and a
    /// longer one would make a genuinely stepping value feel stuck.
    /// </summary>
    public const float QuietMs = 1000f;

    private readonly List<LiveAnnouncement> _pending = [];
    private readonly List<SemanticNode> _scratch = [];
    private bool _seenAFrame;

    /// <summary>
    /// Reads this frame's live regions and queues an announcement for each one whose text differs
    /// from the text it had. Returns nothing: what it produced is drained by the shell, because the
    /// shell is the only thing that knows how its platform posts.
    /// </summary>
    public void Observe(RealizeResult frame, float timeMs)
    {
        var marks = frame.LiveRegions;
        Pending = false;
        for (var i = 0; i < marks.Count; i++)
        {
            var mark = marks[i];
            if (NodeStates.For<LiveRegionState>(mark.Element) is not { } region) continue;
            var text = TextOf(mark);
            // A REGION THAT APPEARS **IS** THE EVENT — on any frame but the first. That is the case
            // `LiveRegion` was added for: a Banner that appears announced nothing (B18) and a Toast
            // lowered to a non-modal Overlay announced nothing (C4), and treating arrival as "no
            // change" would have left both of them exactly as silent as before, through a feature
            // built to fix them.
            //
            // THE FIRST FRAME IS THE EXCEPTION, and only it. A page that reads its own status line
            // aloud on load is the single loudest way to get a reader switched off, and on that
            // frame every region is new by definition — so the first frame SEEDS.
            if (!region.Known && !_seenAFrame)
            {
                region.Known = true;
                region.Said = text;
                continue;
            }
            if (region.Known && string.Equals(region.Said, text, StringComparison.Ordinal))
            {
                region.Pending = false;                   // back to what it said: nothing owed
                continue;
            }
            region.Known = true;
            if (region.SpokeAtMs is { } last && timeMs - last < QuietMs)
            {
                region.Pending = true;                    // owed when the window ends
                Pending = true;
                continue;
            }
            region.Pending = false;
            region.Said = text;
            if (text.Length == 0) continue;               // emptied, not said
            region.SpokeAtMs = timeMs;
            if (_pending.Count == MaxPending) _pending.RemoveAt(0);
            _pending.Add(new LiveAnnouncement(mark.Element.Id, mark.Label, text, mark.Urgency));
        }

        _seenAFrame = true;
    }

    /// <summary>Whether a region of the last frame observed changed inside its quiet window and owes its
    /// text when the window ends: the host keeps asking for frames while it does.</summary>
    public bool Pending { get; private set; }

    /// <summary>
    /// How many announcements may wait to be drained. A BOUND rather than a growing list, because
    /// `PhotonHost` is shared by every shell, and a shell that drains nothing (Windows, until its UI
    /// Automation provider, #310) would otherwise grow the queue for the life of the app; macOS, iOS
    /// and Android drain it after every frame (#1081). When the bound is reached the OLDEST
    /// goes — an announcement is an event whose worth expires, and the same reasoning already
    /// decides what a region says when its quiet window opens: the current state, not the backlog.
    /// </summary>
    private const int MaxPending = 32;

    /// <summary>
    /// Everything queued since the last drain, and the queue is emptied. DRAINED rather than read:
    /// an announcement is an event, and a bridge that polled a list would post the same one on
    /// every poll — which is the sixty-times-a-second failure this whole class exists to avoid,
    /// arriving from the other end.
    /// </summary>
    public IReadOnlyList<LiveAnnouncement> Take()
    {
        if (_pending.Count == 0) return Array.Empty<LiveAnnouncement>();
        var taken = _pending.ToArray();
        _pending.Clear();
        return taken;
    }

    /// <summary>What a reader would say for the region's contents, as one line.</summary>
    private string TextOf(LiveRegionMark mark)
    {
        if (mark.Text is { } words) return words;
        _scratch.Clear();
        SemanticsTree.CollectChildren(mark.Node, _scratch);
        if (_scratch.Count == 0) return string.Empty;
        if (_scratch.Count == 1) return _scratch[0].Label;
        // A region holding a bar and its caption says both, in reading order, the way a reader
        // would voice them one after another.
        return string.Join(" ", _scratch.Where(n => n.Label.Length > 0).Select(n => n.Label));
    }
}
