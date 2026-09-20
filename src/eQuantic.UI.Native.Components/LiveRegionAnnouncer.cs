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
/// WHAT IT COSTS WHEN NOTHING IS LIVE, which is almost every frame of almost every app: one
/// <c>Count == 0</c> test, and when the previous frame had none either, not even a dictionary
/// lookup. That matters because <c>SteadyMotion_WithRecycledFrames_AllocatesFarLess</c> charges a
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
/// when the bar reads 58% is worse than saying nothing.
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

    /// <summary>What each region said, by path. Strings ONLY — a mark's node belongs to a frame
    /// that is recycled the moment the next one lands, so keeping one would be keeping a corpse.</summary>
    private readonly Dictionary<string, string> _said = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _spokeAtMs = new(StringComparer.Ordinal);
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
        if (marks.Count == 0)
        {
            // A region that went away has nothing to announce and nothing to remember. Clearing is
            // what makes it announce AGAIN when it comes back — a toast that reappears with the
            // same words is a new event, and the user was not there for the first one.
            if (_said.Count > 0) Forget(marks);
            _seenAFrame = true;
            return;
        }

        for (var i = 0; i < marks.Count; i++)
        {
            var mark = marks[i];
            var text = TextOf(mark);
            var known = _said.TryGetValue(mark.Path, out var before);
            // A REGION THAT APPEARS **IS** THE EVENT — on any frame but the first. That is the case
            // `LiveRegion` was added for: a Banner that appears announced nothing (B18) and a Toast
            // lowered to a non-modal Overlay announced nothing (C4), and treating arrival as "no
            // change" would have left both of them exactly as silent as before, through a feature
            // built to fix them.
            //
            // THE FIRST FRAME IS THE EXCEPTION, and only it. A page that reads its own status line
            // aloud on load is the single loudest way to get a reader switched off, and on that
            // frame every region is new by definition — so the first frame SEEDS.
            if (!known && !_seenAFrame)
            {
                _said[mark.Path] = text;
                continue;
            }
            if (known && string.Equals(before, text, StringComparison.Ordinal)) continue;
            _said[mark.Path] = text;
            if (text.Length == 0) continue;               // emptied, not said
            if (_spokeAtMs.TryGetValue(mark.Path, out var last) && timeMs - last < QuietMs) continue;
            _spokeAtMs[mark.Path] = timeMs;
            if (_pending.Count == MaxPending) _pending.RemoveAt(0);
            _pending.Add(new LiveAnnouncement(mark.Path, mark.Label, text, mark.Urgency));
        }

        Forget(marks);
        _seenAFrame = true;
    }

    /// <summary>
    /// How many announcements may wait to be drained. A BOUND rather than a growing list, because
    /// `PhotonHost` is shared by every shell and only the macOS one drains today: iOS and Android
    /// run the same host with no bridge wired, so an unbounded queue would grow for the life of the
    /// app on exactly the two targets that cannot yet use it. When the bound is reached the OLDEST
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
        _scratch.Clear();
        SemanticsTree.CollectChildren(mark.Node, _scratch);
        if (_scratch.Count == 0) return string.Empty;
        if (_scratch.Count == 1) return _scratch[0].Label;
        // A region holding a bar and its caption says both, in reading order, the way a reader
        // would voice them one after another.
        return string.Join(" ", _scratch.Where(n => n.Label.Length > 0).Select(n => n.Label));
    }

    /// <summary>
    /// Drops what this frame no longer holds. Without it a long-lived app accumulates a row per
    /// path that ever carried a live region — a list whose rows are toasts is unbounded, and a
    /// leak that only shows up after an hour is the kind nobody attributes to the right feature.
    /// <para>
    /// It used to open with <c>if (_said.Count == marks.Count) return;</c>, which is CORRECT only
    /// while every mark in a frame has a distinct path: the loop above writes each current path
    /// into <c>_said</c>, so equal counts mean nothing stale — unless two marks share one. Paths
    /// are positional and were measured distinct, so nothing reachable broke. It is gone anyway,
    /// because it bought a skipped scan over a handful of keys and cost a correctness argument that
    /// rested on an invariant stated nowhere; if that invariant ever failed, what it would produce
    /// is a stale entry that announces nothing, silently.
    /// </para>
    /// </summary>
    private void Forget(IReadOnlyList<LiveRegionMark> marks)
    {
        foreach (var path in _said.Keys.ToArray())
        {
            var held = false;
            for (var i = 0; i < marks.Count && !held; i++) held = marks[i].Path == path;
            if (held) continue;
            _said.Remove(path);
            _spokeAtMs.Remove(path);
        }
    }
}
