namespace eQuantic.UI.Components;

/// <summary>
/// Turns a <see cref="MermaidGraph"/> into placed geometry. A Sugiyama-lite for flowcharts —
/// longest-path ranks, one barycenter ordering sweep each way, edges as single cubics — and a plain
/// grid for sequence diagrams (participants are columns, messages are rows; declaration order IS
/// the layout, which is the whole charm of the grammar).
/// <para>
/// PARITY IS THE DESIGN CONSTRAINT. This runs as C# on the server and on Photon, and as the
/// transpiled twin in the browser, and the two must place every box on identical numbers — so the
/// math is integers and exact halves throughout: node sizes are whole dp, positions accumulate by
/// addition, and barycenter ORDER is decided by cross-multiplied integer sums
/// (<c>sumA·countB &lt; sumB·countA</c>) so no division ever happens. Text width is an ESTIMATE
/// (characters × a flat advance): honest for a diagram, deterministic everywhere, and free of the
/// realizer-side measurer the layout must not depend on.
/// </para>
/// </summary>
public static class MermaidLayout
{
    private const float NodeHeight = 36;
    private const float RankGap = 48;
    private const float SiblingGap = 32;
    private const float Margin = 16;
    private const float LineThickness = 2;

    public static MermaidScene Solve(MermaidGraph graph) =>
        graph.Kind == MermaidKind.Sequence ? SolveSequence(graph) : SolveFlowchart(graph);

    /// <summary>The flat-advance width estimate: whole dp, clamped to a sane card. Eight dp per
    /// character — the Label role lands near 13–14dp across themes, and the first calibration
    /// (seven) left long node names touching their borders.</summary>
    private static float LabelWidth(string label, float pad, float min, float max)
    {
        var w = label.Length * 8 + pad;
        if (w < min) return min;
        if (w > max) return max;
        return w;
    }

    /// <summary>An edge label's chip width — ONE estimate, used by the layout (to widen the rank
    /// gap the chip sits in) and by the component (to center the chip on its point). Two numbers
    /// would drift, and a drifted chip clips.</summary>
    internal static float LabelChipWidth(string text) => text.Length * 7 + 20;

    private static float NodeWidth(MermaidNode node)
    {
        if (node.Shape == MermaidShape.Circle) return CircleSide(node);
        if (node.Shape == MermaidShape.Diamond) return DiamondSide(node);
        return LabelWidth(node.Label, 32, 72, 280);
    }

    private static float NodeHeightOf(MermaidNode node)
    {
        if (node.Shape == MermaidShape.Circle) return CircleSide(node);
        if (node.Shape == MermaidShape.Diamond) return DiamondSide(node);
        return NodeHeight;
    }

    private static float CircleSide(MermaidNode node) => LabelWidth(node.Label, 24, 48, 130);

    /// <summary>Diamonds are SQUARE by construction: the rhombus is a single-path
    /// <see cref="Primitives.Vector"/>, and a vector draws into a square box on both targets.
    /// Ten dp per character — the label lives in the rhombus' inscribed box, roughly half the
    /// side, so the estimate carries that geometry.</summary>
    private static float DiamondSide(MermaidNode node) => LabelWidth(node.Label, 44, 76, 170);

    // ---- Flowchart ---------------------------------------------------------------------------

    private static MermaidScene SolveFlowchart(MermaidGraph graph)
    {
        var count = graph.Nodes.Count;
        var index = new Dictionary<string, int>();
        for (var i = 0; i < count; i++) index[graph.Nodes[i].Id] = i;

        // Rank on the DAG, not the graph: a cycle's closing edge must not inflate the ranks of
        // everything before it (D --> A would otherwise push A below its own successors). A DFS
        // in declaration order marks edges that land on a node still on the stack as BACK edges;
        // ranking relaxes only the rest, and the back edges simply route upward at draw time.
        var back = MarkBackEdges(graph, index, count);

        var rank = new int[count];
        for (var pass = 0; pass < count; pass++)
        {
            var changed = false;
            for (var e = 0; e < graph.Edges.Count; e++)
            {
                if (back[e]) continue;
                var edge = graph.Edges[e];
                var from = index[edge.From];
                var to = index[edge.To];
                if (from == to) continue;
                if (rank[to] < rank[from] + 1)
                {
                    rank[to] = rank[from] + 1;
                    changed = true;
                }
            }
            if (!changed) break;
        }

        var rankCount = 0;
        for (var i = 0; i < count; i++) if (rank[i] + 1 > rankCount) rankCount = rank[i] + 1;

        // Order within each rank: declaration order first, then one barycenter sweep down (order
        // by predecessors) and one up (by successors). Insertion sort keeps ties stable — a
        // stable order is part of the parity contract, so no library sort is trusted with it.
        var order = new List<List<int>>();
        for (var r = 0; r < rankCount; r++) order.Add([]);
        for (var i = 0; i < count; i++) order[rank[i]].Add(i);

        var position = new int[count];
        for (var r = 0; r < rankCount; r++) Reposition(order[r], position);

        for (var r = 1; r < rankCount; r++)
        {
            SortByNeighbors(order[r], graph, index, position, true);
            Reposition(order[r], position);
        }
        for (var r = rankCount - 2; r >= 0; r--)
        {
            SortByNeighbors(order[r], graph, index, position, false);
            Reposition(order[r], position);
        }

        // Coordinates on the abstract axes: MAIN advances rank by rank, CROSS packs siblings and
        // centers each rank against the widest one. Every operand is a whole number or an exact
        // half, so both compilations of this method agree to the bit.
        var mainSize = new float[count];
        var crossSize = new float[count];
        for (var i = 0; i < count; i++)
        {
            var node = graph.Nodes[i];
            mainSize[i] = graph.Vertical ? NodeHeightOf(node) : NodeWidth(node);
            crossSize[i] = graph.Vertical ? NodeWidth(node) : NodeHeightOf(node);
        }

        var rankSpan = new float[rankCount];
        var rankThickness = new float[rankCount];
        for (var r = 0; r < rankCount; r++)
        {
            float span = 0;
            float thick = 0;
            foreach (var i in order[r])
            {
                span += crossSize[i] + (span > 0 ? SiblingGap : 0);
                if (mainSize[i] > thick) thick = mainSize[i];
            }
            rankSpan[r] = span;
            rankThickness[r] = thick;
        }

        float maxSpan = 0;
        for (var r = 0; r < rankCount; r++) if (rankSpan[r] > maxSpan) maxSpan = rankSpan[r];

        // In LR the label chip lies ALONG the rank gap, so a labeled edge's gap must fit its
        // chip — mermaid.js solves the same collision the same way, by widening the gap. In TD
        // the chip crosses the gap at its own 20dp height and the default depth already fits it.
        var gapAfter = new float[rankCount];
        for (var r = 0; r < rankCount; r++) gapAfter[r] = RankGap;
        if (!graph.Vertical)
        {
            foreach (var edge in graph.Edges)
            {
                if (edge.Label.Length == 0) continue;
                // Each end looked up ONCE. Four dictionary reads to pick the smaller of two ranks
                // is work repeated for nothing, and on the web each read now checks that the key
                // is there (a missing one throws, as it does in .NET) — so a repeat is not free.
                var fromRank = rank[index[edge.From]];
                var toRank = rank[index[edge.To]];
                var lower = fromRank < toRank ? fromRank : toRank;
                var needed = LabelChipWidth(edge.Label) + 24;
                if (lower >= 0 && lower < rankCount && gapAfter[lower] < needed)
                    gapAfter[lower] = needed;
            }
        }

        var mainStart = new float[rankCount];
        var cursor = Margin;
        for (var r = 0; r < rankCount; r++)
        {
            mainStart[r] = cursor;
            cursor += rankThickness[r] + gapAfter[r];
        }

        var crossCenter = new float[count];
        var mainCenter = new float[count];
        for (var r = 0; r < rankCount; r++)
        {
            var at = Margin + (maxSpan - rankSpan[r]) / 2;
            foreach (var i in order[r])
            {
                crossCenter[i] = at + crossSize[i] / 2;
                at += crossSize[i] + SiblingGap;
                mainCenter[i] = mainStart[rank[i]] + rankThickness[rank[i]] / 2;
            }
        }

        var scene = new MermaidScene();
        for (var i = 0; i < count; i++)
        {
            var node = graph.Nodes[i];
            var w = NodeWidth(node);
            var h = NodeHeightOf(node);
            var x = (graph.Vertical ? crossCenter[i] : mainCenter[i]) - w / 2;
            var y = (graph.Vertical ? mainCenter[i] : crossCenter[i]) - h / 2;
            scene.Nodes.Add(new MermaidPlacedNode { Node = node, X = x, Y = y, W = w, H = h });
        }

        foreach (var edge in graph.Edges)
        {
            var from = scene.Nodes[index[edge.From]];
            var to = scene.Nodes[index[edge.To]];
            RouteFlowEdge(scene, graph.Vertical, from, to, edge);
        }

        // The main-axis cursor already starts at Margin, so only ONE margin is added back there —
        // and the trailing gap it carries is the LAST rank's own, not the constant.
        var mainExtent = cursor - gapAfter[rankCount - 1] + Margin;
        scene.Width = graph.Vertical ? maxSpan + Margin * 2 : mainExtent;
        scene.Height = graph.Vertical ? mainExtent : maxSpan + Margin * 2;
        return scene;
    }

    /// <summary>Marks each edge as tree/back via an iterative DFS in declaration order: an edge
    /// landing on a node still ON THE STACK closes a cycle. Deterministic by construction — the
    /// stack replays declaration order, so both compilations mark the same edges.</summary>
    private static bool[] MarkBackEdges(MermaidGraph graph, Dictionary<string, int> index, int count)
    {
        var back = new bool[graph.Edges.Count];
        var outgoing = new List<List<int>>();
        for (var i = 0; i < count; i++) outgoing.Add([]);
        for (var e = 0; e < graph.Edges.Count; e++)
        {
            var edge = graph.Edges[e];
            if (index[edge.From] == index[edge.To]) back[e] = true;
            else outgoing[index[edge.From]].Add(e);
        }

        // 0 = untouched, 1 = on the stack, 2 = done.
        var state = new int[count];
        var stackNode = new List<int>();
        var stackNext = new List<int>();
        for (var root = 0; root < count; root++)
        {
            if (state[root] != 0) continue;
            state[root] = 1;
            stackNode.Add(root);
            stackNext.Add(0);
            while (stackNode.Count > 0)
            {
                var node = stackNode[stackNode.Count - 1];
                var next = stackNext[stackNext.Count - 1];
                if (next >= outgoing[node].Count)
                {
                    state[node] = 2;
                    stackNode.RemoveAt(stackNode.Count - 1);
                    stackNext.RemoveAt(stackNext.Count - 1);
                    continue;
                }
                stackNext[stackNext.Count - 1] = next + 1;
                var e = outgoing[node][next];
                var to = index[graph.Edges[e].To];
                if (state[to] == 1) back[e] = true;
                else if (state[to] == 0)
                {
                    state[to] = 1;
                    stackNode.Add(to);
                    stackNext.Add(0);
                }
            }
        }
        return back;
    }

    private static void Reposition(List<int> rankOrder, int[] position)
    {
        for (var k = 0; k < rankOrder.Count; k++) position[rankOrder[k]] = k;
    }

    /// <summary>Orders one rank by neighbor barycenter WITHOUT division: entry A goes before B
    /// when <c>sumA·countB &lt; sumB·countA</c>. Entries with no neighbors keep their place.</summary>
    private static void SortByNeighbors(List<int> rankOrder, MermaidGraph graph,
        Dictionary<string, int> index, int[] position, bool byPredecessors)
    {
        var sums = new int[rankOrder.Count];
        var counts = new int[rankOrder.Count];
        for (var k = 0; k < rankOrder.Count; k++)
        {
            var node = rankOrder[k];
            var sum = 0;
            var n = 0;
            foreach (var edge in graph.Edges)
            {
                var from = index[edge.From];
                var to = index[edge.To];
                var neighbor = -1;
                if (byPredecessors && to == node) neighbor = from;
                if (!byPredecessors && from == node) neighbor = to;
                if (neighbor < 0) continue;
                sum += position[neighbor];
                n++;
            }
            sums[k] = n == 0 ? position[node] : sum;
            counts[k] = n == 0 ? 1 : n;
        }

        // Insertion sort — stability is load-bearing (see the class remarks).
        for (var k = 1; k < rankOrder.Count; k++)
        {
            var node = rankOrder[k];
            var sum = sums[k];
            var n = counts[k];
            var j = k - 1;
            while (j >= 0 && sums[j] * n > sum * counts[j])
            {
                rankOrder[j + 1] = rankOrder[j];
                sums[j + 1] = sums[j];
                counts[j + 1] = counts[j];
                j--;
            }
            rankOrder[j + 1] = node;
            sums[j + 1] = sum;
            counts[j + 1] = n;
        }
    }

    private static void RouteFlowEdge(MermaidScene scene, bool vertical,
        MermaidPlacedNode from, MermaidPlacedNode to, MermaidEdge edge)
    {
        // Anchor at the flow-side centers; waypoints bend at the middle of the gap between the
        // two anchors, which lands mid-rank-gap for forward edges and still reads for back edges.
        float x0, y0, x1, y1;
        if (vertical)
        {
            x0 = from.X + from.W / 2;
            y0 = from.Y + from.H;
            x1 = to.X + to.W / 2;
            y1 = to.Y;
        }
        else
        {
            x0 = from.X + from.W;
            y0 = from.Y + from.H / 2;
            x1 = to.X;
            y1 = to.Y + to.H / 2;
        }

        var mid = vertical ? (y0 + y1) / 2 : (x0 + x1) / 2;
        if (vertical)
        {
            // ONE cubic whose controls sit on the mid line: the S every flowchart tool draws, and
            // the shape three orthogonal boxes were standing in for. It leaves the source going
            // along the flow and arrives at the target the same way, which is what makes an
            // arrowhead aimed straight at the node still read as aimed.
            AddCurve(scene, x0, y0, x0, mid, x1, mid, x1, y1);
            if (edge.Arrow)
                scene.Arrows.Add(new MermaidArrowhead { X = x1, Y = y1, Direction = y1 >= mid ? 0 : 2 });
            if (edge.Label.Length > 0)
                scene.Labels.Add(new MermaidLabel { Text = edge.Label, X = (x0 + x1) / 2, Y = mid });
        }
        else
        {
            AddCurve(scene, x0, y0, mid, y0, mid, y1, x1, y1);
            if (edge.Arrow)
                scene.Arrows.Add(new MermaidArrowhead { X = x1, Y = y1, Direction = x1 >= mid ? 1 : 3 });
            if (edge.Label.Length > 0)
                scene.Labels.Add(new MermaidLabel { Text = edge.Label, X = mid, Y = (y0 + y1) / 2 });
        }
    }

    /// <summary>
    /// One edge as a cubic: the box that holds it, and the path in the canvas's own coordinates.
    /// <para>
    /// Every number is ROUNDED TO AN INTEGER before it reaches the string. The path is built twice
    /// — by C# for SSR, by the transpiled twin for the browser — and a float that goes through a
    /// machine's current culture reads <c>72,5</c> on half the world's laptops, which is a path the
    /// SVG parser drops and a diagram that renders on one continent.
    /// </para>
    /// <para>
    /// The box is inflated by the stroke's own half-width plus a pixel, because a curve is drawn
    /// ON its path: an un-inflated box clips exactly the outer half of the line, and does it worst
    /// at the ends, where the arrowhead meets it.
    /// </para>
    /// </summary>
    private static void AddCurve(MermaidScene scene, float x0, float y0,
        float c1x, float c1y, float c2x, float c2y, float x1, float y1)
    {
        var sx = Whole(x0);
        var sy = Whole(y0);
        var k1x = Whole(c1x);
        var k1y = Whole(c1y);
        var k2x = Whole(c2x);
        var k2y = Whole(c2y);
        var ex = Whole(x1);
        var ey = Whole(y1);

        // The hull of a cubic bounds the curve, so the control points are enough — no need to
        // solve the extrema for a shape that never leaves its own hull.
        var minX = Min4(sx, k1x, k2x, ex) - CurvePad;
        var maxX = Max4(sx, k1x, k2x, ex) + CurvePad;
        var minY = Min4(sy, k1y, k2y, ey) - CurvePad;
        var maxY = Max4(sy, k1y, k2y, ey) + CurvePad;

        scene.Curves.Add(new MermaidCurve
        {
            X = minX,
            Y = minY,
            W = maxX - minX,
            H = maxY - minY,
            Path = "M " + sx + " " + sy + " C " + k1x + " " + k1y + ", "
                + k2x + " " + k2y + ", " + ex + " " + ey,
            ViewBox = minX + " " + minY + " " + (maxX - minX) + " " + (maxY - minY),
        });
    }

    /// <summary>Half the stroke, plus one — see <see cref="AddCurve"/>.</summary>
    private const int CurvePad = 2;

    /// <summary>The nearest whole number, away from zero at the half — the one rule C# and the
    /// transpiled twin state identically without either side reaching for a rounding mode.</summary>
    private static int Whole(float v) => (int)(v < 0 ? v - 0.5f : v + 0.5f);

    private static int Min4(int a, int b, int c, int d)
    {
        var m = a < b ? a : b;
        if (c < m) m = c;
        return d < m ? d : m;
    }

    private static int Max4(int a, int b, int c, int d)
    {
        var m = a > b ? a : b;
        if (c > m) m = c;
        return d > m ? d : m;
    }

    /// <summary>One orthogonal piece as a thin box (sequence diagrams: a lifeline is straight, and
    /// so is a message); zero-length pieces are simply not added.</summary>
    private static void AddSegment(MermaidScene scene, float x0, float y0, float x1, float y1)
    {
        if (x0 == x1 && y0 == y1) return;
        if (x0 == x1)
        {
            var top = y0 < y1 ? y0 : y1;
            var h = y0 < y1 ? y1 - y0 : y0 - y1;
            scene.Segments.Add(new MermaidSegment
            { X = x0 - LineThickness / 2, Y = top, W = LineThickness, H = h });
            return;
        }
        var left = x0 < x1 ? x0 : x1;
        var w = x0 < x1 ? x1 - x0 : x0 - x1;
        scene.Segments.Add(new MermaidSegment
        { X = left, Y = y0 - LineThickness / 2, W = w, H = LineThickness });
    }

    // ---- Sequence ------------------------------------------------------------------------------

    private const float LifelineTop = 52;
    private const float MessageGap = 44;
    private const float ParticipantGap = 56;

    private static MermaidScene SolveSequence(MermaidGraph graph)
    {
        var scene = new MermaidScene();
        var centers = new Dictionary<string, float>();

        var at = Margin;
        foreach (var participant in graph.Nodes)
        {
            var w = LabelWidth(participant.Label, 28, 80, 220);
            scene.Nodes.Add(new MermaidPlacedNode
            { Node = participant, X = at, Y = Margin, W = w, H = NodeHeight });
            centers[participant.Id] = at + w / 2;
            at += w + ParticipantGap;
        }

        var bottom = Margin + LifelineTop + graph.Messages.Count * MessageGap + Margin;

        foreach (var placed in scene.Nodes)
        {
            var x = placed.X + placed.W / 2;
            AddSegment(scene, x, placed.Y + placed.H, x, bottom - Margin);
        }

        var y = Margin + LifelineTop + MessageGap / 2;
        foreach (var message in graph.Messages)
        {
            var x0 = centers[message.From];
            var x1 = centers[message.To];
            if (x0 == x1)
            {
                // A self message loops out and back: right, down, and home.
                AddSegment(scene, x0, y - 12, x0 + 36, y - 12);
                AddSegment(scene, x0 + 36, y - 12, x0 + 36, y);
                AddSegment(scene, x0 + 36, y, x0, y);
                scene.Arrows.Add(new MermaidArrowhead { X = x0, Y = y, Direction = 3 });
                if (message.Label.Length > 0)
                    scene.Labels.Add(new MermaidLabel { Text = message.Label, X = x0 + 36 + 12, Y = y - 12 });
            }
            else
            {
                AddSegment(scene, x0, y, x1, y);
                scene.Arrows.Add(new MermaidArrowhead { X = x1, Y = y, Direction = x1 > x0 ? 1 : 3 });
                if (message.Label.Length > 0)
                    scene.Labels.Add(new MermaidLabel
                    { Text = message.Label, X = (x0 + x1) / 2, Y = y - 12 });
            }
            y += MessageGap;
        }

        scene.Width = at - ParticipantGap + Margin;
        scene.Height = bottom;
        return scene;
    }
}
