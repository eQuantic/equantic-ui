# Design

## Context

The web realizes an `AdaptiveNode` as a `display: contents` wrapper holding one gate per declared
arm. A gate's class names its range in dp (`eq-vc600` is compact until 600, `eq-vm600-840` medium
from 600 until 840, `eq-vx840` expanded from 840), and its rules, derived from the name, switch it
between `display: contents` and `display: none`. The server and the browser derive the same name and
the same rules from the same thresholds, so hydration compares equal. Photon measures the one arm
`Resolve(SizeClass)` picks, in the node's place, and paints nothing of the others.

## How Flutter solves it

`docs/FLUTTER-PARITY.md` names `LayoutBuilder` as what `AdaptiveNode` is the window-class case of
(row 59), and the placement half is Flutter's parent data (row 46). `Positioned`, `Flexible` and
`Expanded` are `ParentDataWidget`s: they write the parent data of the nearest render object below
them, and a widget that builds its child without a render object of its own is transparent to them.
A switcher that returns one of its children is such a widget, so the child it picked is placed by the
switcher's parent exactly as a direct child would be, and the switcher carries no parent data of its
own, since it is never laid out. That is the rule here, on both targets: an arm is laid out where it
stands.

## Decisions

**The point is an underscore in the name.** #669 offered three ways out. Escaping the dot in the
selector (`.eq-vc703\.7037`) needs an escaping rule in each twin and leaves a name every other reader
of it (`querySelector`, the runtime's own question whether an arm is shown) must escape the same way.
Rounding a threshold to a whole dp moves a breakpoint the author wrote, and makes the web and Photon
disagree about where the node switches. An underscore keeps the name one identifier, so the selector
is the name and nothing escapes anything.

**The threshold is an integer before it is a text.** `AdaptiveGates.Units` and `gateUnits` round
`(double)dp * 10000`, which is exact for every single, away from zero, so both producers hold the same
integer for the same threshold by construction; spelling it is integer arithmetic, and so is the
0.02dp a medium range closes short of the next threshold. C#'s `"0.####"` of a float, which the name
used to be, rounds to seven significant digits before it rounds to four decimals, and that is where
1066.6667 became `1066.667` on the server. Four decimals is the resolution the name always had, so a
whole threshold keeps its name, and a fractional one keeps the digits the TypeScript twin already
wrote, with an underscore where its dot was.

**One rule on each side of the web: the parent's.** `Place` (C#) and `placeChild` (TS) take a
container's rule for a direct child, and lower an AdaptiveNode by lowering each arm with that same
rule, inside its gate. The flex passes its axis and align-self, the grid its span, the stack its
anchor or cell, and a stack resolves a component first, since a component may build the node. Any
other parent reaches the node through its own door, which lowers the arms on the axis the node was
given. The wrapper is still the node's element, so `Decorate` writes the node's key, bookmark and
origin on it, apart from the dispatch. Rejected: cases per node kind inside `LowerAdaptive`, a Spacer
given an axis there and a Positioned an anchor. The placement belongs to the parent, and every
container with a rule for its children would have needed one more.

**Photon reads the arm where it reads the child.** `LaidOutChildren` is a struct over a container's
list that answers the resolved arm in an AdaptiveNode's place, and the path the node's own
measurement gives that arm, so nothing remembered by path moves and nothing is allocated. A line and
a grid read through it. The frame got cheaper on the way: the line asked its node for its children
with `foreach`, which boxes the list's enumerator on every measure, and the perf harness's pooled
frame is 71.1 KB where it was 73.2 (73.2 again with that one loop put back), 506 bytes a layer
either way. A Stack asks one question, whether a child is Positioned, after measuring it, so
`PositionedOf` asks the measured node first, which already is the arm, the way it already read
through a component. `LayoutContext.ArmOf` is the one place layout decides which arm a node is.

**The node's own placement is not read.** An arm carries its own `AlignSelf` and `GridSpan`, and the
node is never laid out, so on both targets nothing reads the node's own. Photon did read them, and
the web wrote them on a `display: contents` wrapper, where they did nothing. Inheriting the node's
own into an arm that says nothing was the alternative: it keeps an app that set one on the node
working on Photon, at the price of a second source for one property, and nothing sets them.

## Risks / Trade-offs

- An app that set `AlignSelf` or `GridSpan` on an AdaptiveNode loses it on Photon. The migration line
  says to set it on the arm.
- A threshold is honoured to the ten-thousandth of a dp, which is the resolution its name always had.

## Found while measuring, not changed here

- Photon resolves an AdaptiveNode by the spec's size class and ignores `MediumFrom` and
  `ExpandedFrom`: at 900dp an `ExpandedFrom = 980` node shows its expanded arm on Photon and its
  compact arm on the web, and a node switching at 703.7037 switches at 840 there. `ArmOf` is where
  the answer belongs (`ResolveWidth` against the window).
- On Photon a component that builds a Spacer takes no space in a column, where the web keeps it: the
  line asks the component, not what it builds, whether it is a Spacer.
