# Design

## The strings a page reads (#1782)

eqc already knows, per module, which catalogue keys it reads: each resource use it lowers to a lookup. It
records them on the module's compilation (`CompilationResult.StringKeys`), and after the build claims the
modules it writes, walks each page and the layout through the modules they reach: a module's imports of
its siblings, the edges the bundler follows. The factory surface (`AppUI`) is the exception, since it
imports every component of the app: a module reaches the component a factory it CALLS constructs, and no
other. The index (`page-strings.json`) is `{ whole: [ids], pages: { Module: [keys] } }`, where `whole`
names the catalogues the build ships to every page (the SDK's own, read by the runtime's components).

The server reads the index (cached by its stamp) and inlines, for a document, the page's and the layout's
keys plus every `whole` catalogue; for a navigation, the page's. A page missing from the index (a build
older than the server, a page eqc did not write) gets the whole catalogue, so nothing regresses into a
missing string. In the browser, a key not in the strings handed over is fetched with the rest of its
catalogue (`completeActiveCatalog`, once per culture) before the runtime says it is missing.

## One arm in the browser (#1783)

The question Flutter answers: its framework has no media gates, so a widget tree holds one arm, chosen by
`MediaQuery`, and a resize rebuilds. Photon already works so. The web held every arm for two reasons: the
server cannot know the window's width, and a page must paint before its script runs. Both hold only until
hydration.

So the browser's live owners hold every arm while the page hydrates (the markup the server wrote is adopted
as it is), and then the document flips to one arm (`adaptive-window.ts`): the boot calls `holdOneArm()` and
renders the page once, which every live owner reads in `buildTree` (`owner.holdsEveryArm`). That render
takes the hidden arms out and keeps the shown one where it stood. A tree laid out once (`structuralOwner`)
keeps every arm behind its gates, since nothing renders it again when the window changes.

The arm stands at its OWN slot on every target (`AdaptiveNode.ResolveIndex`): compact at 0, medium at 1,
expanded at 2, where Photon used to hold the one arm at 0. A position's path (`SlotsBelow`) is how the
server names a component's data and how hydration finds it, so the arm the browser keeps after the flip is
the position the server named, and Photon's tree reads the same way.

Lowering: where the owner holds one arm, the node's position holds exactly one child, and that arm is
lowered alone, in a box with no gate. The engine resolved it for the window's width; a gate whose media
query disagreed by a fraction of a pixel (a zoomed window, whose `innerWidth` is rounded) would show
nothing. To keep the two from disagreeing at all, the width handed to the owner is `innerWidth` kept on the
side of each known threshold that the threshold's own media query says (`adaptiveWindowWidth`). Every arm's
box is keyed `arm{index}`, in both modes and on both web producers, so the render after hydration keeps the
shown arm's DOM (a video or an iframe in it does not reload) and a crossing swaps one box for the other.

A window crossing a threshold: each threshold the page's nodes switch at is watched through `matchMedia`,
with the media condition the gates use (`adaptiveThresholdMedia`), and a change renders the page again,
only once one arm is held.

An arm of the same type and key as the one shown takes its position across a threshold
(`NodeElement.CarryTheArmAcross`): Photon kept that state while it held every arm at slot 0, and moving the
arm to its own slot must not lose it.

## The global key (#797)

Flutter's mechanism, as written: `GlobalKey` is an identity (the instance), and `Element.inflateWidget`
asks `_retakeInactiveElement` for an element of that key before making one. The element may be INACTIVE
(it left its parent earlier in this build) or still ACTIVE elsewhere (its old parent has not been reached
yet); in the second case it is taken from its parent (`forgetChild`), which must build again in the same
frame.

Here:

- `VisualNode.GlobalKey` is part of `CanUpdate`: two nodes of one type with different keys are different
  positions.
- `BuildOwner` records the positions of each key (`Hold`/`LetGo`, at mount and at the final leave).
- A position with a key that leaves its parent during the frame's build is PARKED whole
  (`Element.Deactivate` → `BuildOwner.Park`): its subtree stays as it is, and nothing unmounts.
- `Element.Inflate` asks `BuildOwner.Retake` first. A parked position of the key is taken back; otherwise,
  where one arm is held, the one position of the key that still stands elsewhere is taken from its parent
  (`ForgetChild`), and the parent is remembered. Either way the position is reattached (`Reattach`: parent,
  slot, depth, the inherited value above it, recomputed down the subtree) and updated with the new node.
- A component moved under another inherited value lets go of what it read and hears that its dependencies
  changed, as Flutter's `activate` tells it.
- At the end of the build, every parked position no node took leaves for good (its components unmount at
  the frame's end, as any position's do), and then a parent a position was taken from that still places a
  node there builds that slot again.
- A position moves once in a build, and a position mounted in this build is placed: a second node of the
  key in the same frame starts again, with a development notice. Where every arm is held (the server, the
  browser before hydration), a key repeated across arms is the design, and only parked positions are taken.
- Parking and taking happen only in the frame's build: a layer the realizer shows later, and a tree taken
  down, leave as they always did.

Alternatives weighed:

- **Taking only parked positions.** Simpler, and right when the build reaches the old place first; wrong
  the other way round, which is exactly a window narrowing back to an earlier arm (the column is built
  before the row leaves). Rejected.
- **The arm's position carrying the state without a key.** It covers arms of one type and nothing deeper;
  kept for that case (`CarryTheArmAcross`), and the key covers the rest, as in Flutter.
