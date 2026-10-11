# Design

**Ask what was measured, not what was written.** The classifier behind the item and child split read
a Flexible's child as written, so every node that stands for another one needed an answer of its
own: a transparent wrapper, an Anchored, and now an AdaptiveNode, whose arm is what Photon lays out.
A list of such answers is how the classifier went incomplete before (round two found five node types
it did not read). The measured tree has none of them to answer for: a wrapper's node holds the node
of what it wraps, and an AdaptiveNode's door hands back the arm's node, so the AdaptiveNode is not
in the tree at all. `SizedBy` walks the measured tree through the nodes that take their one child's
size, the same statement the classifiers already made about transparent wrappers and Anchored, and
the per-type classifiers answer for the node it reaches. No AdaptiveNode case is added anywhere.

**The ceiling is decided after a measure.** A scroller's width is capped in its item only when it is
a scroller, which the slot can no longer know before it measures. So the slot measures the child,
and when the node it measured to is a scroller wider than the item, measures it again under the
ceiling, as the wrapping pass already did for a line that held still. That costs one more measure,
only for a scroller wider than its item.

**The kept measure registers the range.** Measuring again was already how the wrapping pass capped a
scroller, and how a line that resolves re-wraps its items, but the scroll range a measure registers
went in with `TryAdd`, so the first measure's range stood: a scroller drawn at 300 around 800 of
content stopped scrolling at 400. Every measure the engine makes again replaces the node it measured
before, and a measure that only probes (a grid's auto track) comes before the one it keeps, so the
last range registered is the one of the node that is drawn.
