# One door per node

> The plan for step 2 of [ARCHITECTURE-AUDIT.md](ARCHITECTURE-AUDIT.md): the dispatches over the
> vocabulary become one visitor per DISPATCH — one for each realizer, and two for email, whose HTML
> and plain-text halves are two dispatches today — so that a node added to the vocabulary is a COMPILE
> ERROR in every realizer until it is handled or declined, in code, with the reason beside it.
> Measured on 2026-09-14 against `main` after #120, and re-measured the same day at 0bd46d0b (#138)
> for the slice table and the two pin rules that S1 taught; the numbers are dated by those lines.

## The problem, in numbers

The SDK has one visual vocabulary — 39 concrete `VisualNode` types plus the `UiComponent` seam — and
seven places that decide what each word means: the six the coverage pin reads, below, and a seventh
it does not — `EmailRenderer.WalkText`, the walker that writes an email's `text/plain` half. Each is a
`switch` over the node type, written by hand, and five of the six pinned ones answer an unknown node
with silence.

| Dispatch | Method | Arms | On an unknown node |
|---|---|---|---|
| `LayoutEngine` | `MeasureCore(node, constraints, ctx, path)` | 37 of 39 | a zero-sized box |
| `WebRealizer` | `LowerNodeKind(node, context, horizontalAxis)` | 37 of 39 (39 once #121 lands) | `null` |
| `lowering.ts` | `lowerNodeKind(node, context, horizontalAxis, path)` | 39 of 39 | `render()` or `null` |
| `PhotonRealizer` | `EmitNode(laidOut, theme, mode, builder, …)` over `laidOut.Source` | 28 of 39 | nothing |
| `Semantics` | `Walk(laidOut, nodes)` | 13 of 39 | the children are walked |
| `EmailRealizer` | `Write(node, context, html)` | 6 of 39 | **throws**, naming the node |

Seven defects came through the silent five; the audit's ledger lists them. What holds the line today
is `VocabularyCoverageTests`: a regex over each method's source, an exemption list per dispatch with
a reason per entry, three assertions in both directions. It is an instrument, and it has the limits
of one — it reads the six methods it is told about and not the seventh, it credits text shapes rather
than semantics, and it moved twice under review before it stopped crediting an arm in one method for
a claim about another. The structural fix makes the compiler do that job for all seven, and retires
the regex.

## How Flutter solves it, and why the copy is not verbatim

Flutter does not dispatch. Every `RenderObject` carries its own `performLayout`, `paint` and
`hitTest` as abstract members, so a node without them does not compile; the framework never asks
"which node is this". That works because Flutter's rendering layer is the only realizer and sits
BELOW the widgets.

Ours is the other way up, deliberately: the vocabulary is written once and realized three times, so
it sits below the realizers and must not see them. A node cannot carry `Lower()` without
`Primitives` referencing `Web`. The .NET shape of the same guarantee — one method per node, missing
one is a compile error, the logic lives outside the hierarchy — is double dispatch: the **Visitor**.

Two patterns for two shapes of problem, and the shape decides. The transpiler's constructs are an
OPEN set (C# syntax, extended by every language version), so it uses Strategy with a registry and a
coverage suite. The vocabulary is a CLOSED set of 39 types we own, so it gets a visitor and the
compiler. One pattern applied to both would be wrong for one of them.

## The design

### The contract, in `Primitives`

```csharp
/// One method per concrete node, and one for the expansion seam. Nothing here has a default body:
/// a realizer that does not know a node does not compile, which is the entire point.
public interface IVisualNodeVisitor<TState, TResult>
{
    TResult Visit(Box node, TState state);
    TResult Visit(Row node, TState state);
    TResult Visit(Column node, TState state);
    // … 36 more, one per sealed node …
    TResult Visit(UiComponent node, TState state);
}

public abstract class VisualNode
{
    public abstract TResult Accept<TState, TResult>(IVisualNodeVisitor<TState, TResult> visitor, TState state);
}

public sealed class Box : VisualNode
{
    public override TResult Accept<TState, TResult>(IVisualNodeVisitor<TState, TResult> visitor, TState state)
        => visitor.Visit(this, state);
}

public abstract class UiComponent : VisualNode
{
    // Sealed: every component the SDK or an app writes is visited as the seam it is, and expanded
    // by the realizer through BuildContained. No app ever writes an Accept.
    public sealed override TResult Accept<TState, TResult>(IVisualNodeVisitor<TState, TResult> visitor, TState state)
        => visitor.Visit(this, state);
}
```

**Overloads, not `VisitBox`.** Inside `Box.Accept`, `this` is statically a `Box`, so `visitor.Visit(this, state)`
binds to `Visit(Box, TState)` at compile time — no reflection, no runtime type test. It reads as the
switch arm it replaces. The rule against overloads elsewhere in this repo is about the TRANSPILED
surface, which JavaScript cannot overload; nothing here is transpiled — eqc never sees `Accept`, and
the TypeScript twins dispatch by wire kind (below).

**Static façades stay; the visitor is the instance behind them.** The five C# entry points are
static classes today — `WebRealizer.Lower(node, theme)`, `LayoutEngine.Layout(root, …)`,
`PhotonRealizer.Realize(…)`, `SemanticsTree.Collect(frame)`, `EmailRealizer.Lower(node, theme)` — and
they remain the public API. A static class cannot implement an interface or hold pass state, so each
façade constructs the visitor for the pass — `sealed partial class WebLowering :
IVisualNodeVisitor<bool?, HtmlElement?>` and its siblings — hands it the pass-fixed inputs, and calls
`root.Accept(visitor, state)`. The switch's helper methods become the visitor's members; the façade
keeps its signature, so no caller changes and the output pins do not know anything happened.

**A state parameter, because every dispatch carries one.** The six methods do not take a node alone;
each carries per-call state a visitor instance cannot hold, because it changes as the recursion
descends. What lives on the visitor instance is what is fixed for the pass.

| Realizer | Per-call state → `TState` | Fixed for the pass → visitor fields | `TResult` |
|---|---|---|---|
| `LayoutEngine` | `readonly record struct MeasureState(LayoutConstraints Constraints, string Path)` | `LayoutContext` | `LayoutNode` |
| `WebRealizer` | `bool? HorizontalAxis` | `ComponentContext` | `HtmlElement?` |
| `PhotonRealizer` | the `LayoutNode` being painted (the visitor visits `laidOut.Source`) | theme, mode, builder, input sink, scroll meta, press and motion scopes, overlay queue | `Nothing` |
| `Semantics` | the `LayoutNode` | the node list | `bool` — whether to descend into the children |
| `EmailRealizer` | `Nothing` | `ComponentContext`, the `StringBuilder` | `Nothing` |
| `EmailRenderer.WalkText` → `EmailTextVisitor` | `Nothing` | the plain-text `StringBuilder`, the theme | `Nothing` — the same contract as the HTML visitor, one instance per message, and one refusal set shared by both |

`Nothing` is a one-member `readonly struct` in `Primitives` for the visitors that produce no value;
`void` is not a type argument in C#.

**No default bodies — and what a decline looks like.** The audit's finding was that a deliberate
omission and a forgotten one looked identical. With abstract methods the forgotten one does not
compile, and the deliberate one is a method whose body says why:

```csharp
// PhotonRealizer — the realizer paints LAID-OUT nodes; a container arrived as geometry with children.
public Nothing Visit(Stack node, LayoutNode laidOut) => PaintedByChildren(node);
public Nothing Visit(Grid node, LayoutNode laidOut) => PaintedByChildren(node);

// EmailRealizer — the medium; the one dispatch whose decline was always loud, and stays loud.
public Nothing Visit(ScrollView node, Nothing _) => Refuse(node, "the medium has no scrolling");
```

`PaintedByChildren`, `Refuse` and their siblings are private helpers on each visitor, named for the
REASON, so that the exemption lists of `VocabularyCoverageTests` — which today carry the reasons in
comments — move into code, one line per node, where the compiler sees the node and the reviewer sees
the reason. Eleven such lines in Photon, twenty-six in Semantics, thirty-three in Email. That is the
honest cost of exhaustiveness, and it is paid once.

### One file per family, in every visitor

The audit measures `WebRealizer.cs` at 2,629 lines with 42 `Lower*` methods, `LayoutEngine.cs` at
1,948 with 18 `Measure*`, `PhotonRealizer.cs` at 1,805 with 13 `Emit*`. A visitor is a `partial class`,
and the transpiler already shows the folder shape (`Strategies/Expressions/`, `Strategies/Statements/`,
one file per construct). The vocabulary's four families are the split:

| Family | Nodes | File |
|---|---|---|
| Containers and layout | `Box`, `Row`, `Column`, `Grid`, `Stack`, `Positioned`, `Flexible`, `Spacer`, `SafeArea`, `Pinned`, `ScrollView`, `AdaptiveNode`, `Anchored`, `Overlay` | `…Visitor.Containers.cs` |
| Text and surfaces | `Text`, `TextEntry`, `CodeSurface`, `SheetSurface` | `…Visitor.Text.cs` |
| Graphics | `Icon`, `Vector`, `Drawing`, `Image`, `Canvas`, `Spinner`, `CameraPreview`, `WebFrame` | `…Visitor.Graphics.cs` |
| Interaction and motion | `Pressable`, `Link`, `Hoverable`, `Adjustable`, `Navigable`, `Shortcut`, `Draggable`, `DragDismiss`, `Presence`, `LoopMotion`, `InView`, `InFlow`, `Simulated` | `…Visitor.Interaction.cs` |
| The seam | `UiComponent` | `…Visitor.cs` (the class itself, with the pass state and the helpers) |

Fourteen, four, eight, thirteen and one: forty. The helper methods each arm calls today
(`LowerBox`, `MeasureFlex`, `EmitText`, …) move with their arm and lose the prefix that named the
switch they were called from.

### The TypeScript side: a generated union and an exhaustive switch

`lowering.ts` dispatches on the wire kind (`case 'box':`), and TypeScript can make that exhaustive at
compile time if the kind is a union type rather than `string`. The union is GENERATED, by the tool that
already writes `enums.generated.ts` and `design-system.generated.ts` from the assembly:

- `NodeKindTsGenerator` in `eQuantic.UI.Web.Build` reads every concrete `VisualNode` type, takes its
  `NodeKind` off an uninitialized instance (the way `VocabularyCoverageTests.WireKind` does), adds the
  expansion seam EXPLICITLY — `UiComponent` is abstract, so no scan of concrete types reaches its
  sealed `"component"` — and FAILS on the three things `EveryNode_DeclaresItsOwnWireKind` fails on
  today: a duplicate kind, an EMPTY kind (which would generate `''` into the union), and a concrete
  node claiming the seam's word. Those three assertions move from the pin into the generator, so S8
  deletes a file and loses no check. Then it writes `node-kinds.generated.ts`:
  `export type NodeKind = 'adaptive' | 'adjustable' | … | 'webFrame' | 'component';`
- A byte-pin test beside `EnumUnionsTsGeneratorTests`, regenerated behind the same environment
  variable, so the file cannot drift from the assembly.
- `nodes.ts` declares `nodeKind: NodeKind` on `VisualNodeValue`, and `vocabulary.ts` declares it on the
  runtime `VisualNode` base class, which types it as `string` today (lines 42–44) — the classes the
  transpiled components construct. Both, or a `VisualNode`-typed value is a way around the union.
- `lowerNodeKind` moves its mixing seam — a web component with no `nodeKind` that renders itself —
  AHEAD of the switch, and ends the switch with `default: return assertNever(node.nodeKind);`, so a
  kind added to the union with no case is a type error in the runtime's build.

### Cost, and what does not change

- **Performance, as a measured risk rather than a premise.** `Accept` is called through `VisualNode`
  at every entry point, so the dispatch is polymorphic by construction — one virtual call to reach the
  node's `Accept`, one interface call back into the visitor — where a `switch` over type patterns is
  a chain of type tests. Five `(TState, TResult)` instantiations exist in the tree, and Native AOT
  (Primitives is `IsAotCompatible`) compiles generic virtual methods, but nothing here promises the
  JIT devirtualizes anything. What the design does guarantee is no PER-NODE allocation: state is a
  `readonly record struct` or an object the pass already owns, and the visitor is a class held for
  the life of the pass — constructed once per pass at most, and in the frame-driven realizers kept
  as a field on the host and rebuilt only when a pass-fixed input changes, so a steady frame allocates
  none. A `struct` visitor would not help: passing it through `IVisualNodeVisitor<,>` boxes it once
  per call. `PerfHarnessTests` pins managed bytes per steady-state frame under a ceiling and is the
  net for all of this — visitor construction included — and the first slice on a hot path (S5, the
  layout engine) is where the frame time is measured before and after, and written into this
  document.
- **The hierarchy is closed by construction, not by counting.** Zero classes outside `Primitives`
  derive `VisualNode` today (measured in `src/` and `samples/`; the only hits are two fakes in a
  transpiler test's source snippet) — and that is a measurement, not a guarantee. A public abstract
  class can be derived from anywhere, and an app's own `Accept` could route to any overload and walk
  around the compiler. So S1 gives `VisualNode` a `private protected` constructor, and gives
  `FlexNode` one too: it is the one public abstract class between `VisualNode` and the leaves, it
  declares no constructor of its own today, and the accessible one it inherits is a second door that
  closing the first would leave open. The 39 concrete nodes are already `sealed`, and nothing in
  `src/`, `samples/` or `tests/` derives from one. Only the vocabulary's assembly can add a node,
  which is the closed set the visitor depends on. `UiComponent`, whose constructor stays `protected`,
  remains the one door open to apps, and its `Accept` is sealed, so no consumer writes one and eqc
  never meets one. A pin holds the closure — `ClosedHierarchyTests` — over every assembly of both
  graphs, not over `Primitives` alone (one assembly reports nothing about an intermediate born
  elsewhere; #135's `ValueShapeCollisionTests` found `VectorTransform`'s twin in the engine that way):
  every type assignable to `VisualNode` outside `Primitives` is a `UiComponent`, every abstract node
  inside it that is not the component seam has only `private protected` constructors, and every
  concrete node is `sealed`.
- **A closure-shaped pin derives its scan; it never lists it.** S1 was specified with a list of eight
  assemblies. A guard comparing the list with what was loaded found two more at once, then five when
  the whole suite ran instead of one filtered test — what the AppDomain holds depends on which tests
  executed first, and a pin whose answer changes with the run order is worse than the list it checks.
  `ClosedHierarchyTests` reads its set from the test's own output directory instead: every
  `eQuantic.*.dll` there that references the vocabulary. Independent of run order, and it grows with
  the tree on its own — with one caveat a directory listing cannot escape: an incremental build leaves
  behind the assembly of a project or reference that was removed, and the pin would load history. A
  clean output has no such file; the stale-proof criterion is the test's own dependency manifest
  (`*.deps.json`, `DependencyContext.Default`), which names exactly the assemblies of THIS build, and
  that is the follow-up for the pin. Every later pin of this shape — S8's replacement for the coverage
  pin included — states the CRITERION for what it scans, reads it from the build's manifest rather than
  from a folder, and asserts that both graphs are in the result.
- **A pin is A/B'd inside the graph it guards.** The proof that `ClosedHierarchyTests` discriminates
  was first written by declaring a stranger node in the test project, and the pin stayed green — test
  assemblies are excluded on purpose, so the A/B was never in the condition it claimed to test. Redone
  inside `eQuantic.UI.Web`, it failed and named the type. Same shape as the CoreText assertion of #136,
  where every other test passed against a middle cut because none asked which side survived. And a
  third, from the same slice: the fence on `Accept` was first proved with a `VisualNode` receiver and
  PASSED, because that binds to the abstract declaration carrying the attribute directly; with a `Text`
  receiver it failed — the same measurement, two receivers, opposite answers, and only the concrete one
  in the condition the fence exists for. So each slice's net includes one A/B written where the defect
  would live: a node with no `Visit` in the realizer's own assembly, not in a test's; a call through the
  concrete node, not the abstract. And the same rule from the other end (#145): a fix for a
  host-dependent line break cannot be exercised on the host where `Environment.NewLine` is already
  `\n`, so its guard asserts the property directly — no CR in the runtime's committed `shared/`
  artifacts (`.ts`, `.json`, `.txt`), the writer breaks
  lines with LF, no source names a construct that asks the host — rather than the fix's effect. An
  instrument that can only pass where it runs is not an instrument.
- **An assertion names every row it claims.** #141's import check was `NotContain("Matrix2D")` as a
  literal, so the rows added for `Nothing` and `SemanticNode` proved the diagnostic — the loud half —
  and said nothing about the import they were added for, the quiet half. It is a theory parameter
  now, and the A/B says why that matters: removing `[ServerOnly]` from `SemanticNode` fails exactly
  one case; before, zero. The same shape sat in the location probe, which named two of the three types
  that moved and would have left `SemanticCheck` behind with the row green. A pin over a SET of things
  is parameterised over the set; one literal standing for the set is the exemption list this plan
  exists to retire, written in a different syntax.
- **When a pin cannot be A/B'd cleanly, say which weaker thing was checked.** The location probe
  could not be made to fail without moving a type to another assembly, so #141 pointed it at the OLD
  assembly, watched the row fail, and wrote that down: it proves the probe reads real assemblies
  rather than a tautology, which is less than "it discriminates" and more than nothing. A net that
  states its own ceiling can be raised later; one that claims the full proof it did not do cannot.
- **Re-measure after the LAST edit, not after the last edit you remember.** #143's body said the
  TypeScript suite was green; it had been, on a tree that no longer existed — the suite ran, THEN the
  transpiled twins were regenerated, and the cross-pin spec still reading `b.x/y/width/height` was
  failing two cases when review looked. This repository already runs a packaging step twice because
  the second run reads the first one's output; a regenerated fixture is the same thing. So a slice's
  proof is the run whose inputs are the commit being reviewed, and a PR body names that run's head.
- **A twin of a `float` rounds where its subject rounds.** #143's transpiled hit test was
  `Math.fround(b.x + b.width + slack)`, because eqc emits `fround` for arithmetic on `float`; reaching
  for the geometry twin's `Rect.inflate().right` dropped it in silence, because the twin did DOUBLE
  arithmetic where its subject has floats, and a pointer exactly on a fractional edge could land on
  different sides on the two targets. The twin rounds at storage and at every step now, cross-pinned
  on fractional values. Two rules for every value-type twin S7 generates or the fixtures pin: the
  discriminating case is FRACTIONAL — `Rect(0.1, 0.2, 0.3, 0.4).Inflate(0.05).Right` is `0.45000002`
  in floats and `0.45` in doubles, and no whole number can show it; and a fixture carries a float
  WIDENED to double, because .NET prints a float as the shortest string that round-trips as a float
  (`0.1f + 0.3f` prints `0.4`, the number is `0.4000000059604645`) while JavaScript prints the number,
  so a fixture in .NET's spelling fails a correct twin. Two instruments, two encodings, decided before
  the first file is written: a cross-pin FIXTURE carries a number, so it widens a float to double and
  the twin's side reads the number; a public-surface BASELINE (brief G) carries a signature, so it
  records a constant or a default exactly as Roslyn displays it and the test compares the text —
  there, `0.4f` is a spelling to hold, not a value to compute, and regeneration can never turn it into
  an argument.
- **A pin that formats in order to agree, agrees; pin the bound, not a point beside it.** The
  layout cross-pin printed both sides to three decimals so they would share one text — exactly one
  rounding coarser than the bit in question, which is how a one-ULP divergence (#146, a `float`
  unrounded at the RETURN seam) lived under a green pin. And the first probe written for it could not
  fail at all: it derived its coordinate from the same edge the comparison used, so the two moved
  together and a twin entirely in doubles still answered zero. A cross-pin compares the numbers the
  subject produces, at the subject's own precision, and it pins the BOUND itself — the edge, the
  width, the returned value — never a quantity computed from that bound on both sides.
- **Output is byte-identical, by slice.** Each realizer already has the pin that says so: the web has
  `ComponentParityFixtureTests`, `PrimitiveValueFixtureTests` and `MarkerParityTests` (and
  `SurfaceSsrTests` once #121 lands — it is that PR's, not `main`'s yet);
  Photon has 91 goldens under `AbstractNodeGoldenTests` and `GoldenSceneTests`, `TreeGpuParityTests`
  and `PerfHarnessTests`; layout has `FlexLayoutTests` and `LayoutCompositeTests`; the browser has 79
  spec files in `shared/` and the transpiled fixtures; email has its 48 facts. A slice that changes a
  pixel or a byte has done something this plan did not ask for.
- **The inner switches are not visitors — with one exception.** `MinContentWidth`, `Shrinkable`,
  `WidthKind`, `CrossSizeKind`, `PositionedOf` in the layout engine and `TextContentOf`, `CapsAt`,
  `Fills`, `ResolveForPositioning` in the web realizer ask questions ABOUT a node — does it shrink,
  what is its width kind, is it transparent to layout. Step 4 of the audit hoists those onto the
  vocabulary as properties; they are answered by the node, not visited. `EmailRenderer.WalkText` is
  the exception: it is a second node-type DISPATCH in the email realizer, the one that writes the
  `text/plain` alternative, and a node the HTML visitor learns to write while this walker does not
  would ship a message whose plain half silently drops it. It becomes a visitor in S3, beside the
  HTML one, and the two share their refusal set so they cannot disagree about what the medium
  carries.

## Slices

Each slice is one PR, sized for review, and lands with its dispatch's output pins untouched. The
executor takes them in this order; the auditor rewrites the audit's section 2 and shrinks
`VocabularyCoverageTests` as each lands.

| # | Slice | Nets | Size |
|---|---|---|---|
| S1 | **Landed (#138).** `IVisualNodeVisitor<,>`, `Nothing`, `Accept` on `VisualNode`, forty one-line overrides, and the `private protected` constructors on `VisualNode` and `FlexNode` that close the hierarchy. No consumer yet. `Nothing` is a public struct, so the runtime's export pin asked about it: it is `[ServerOnly]`, which since #135 the transpiler's fence honours in all seven ways a symbol can be named — a TYPE POSITION included, so a component property typed `Nothing` is a build error, not an `import` from the runtime. `Accept` itself is `[ServerOnly]` too, on the abstract declaration only: it is public on a runtime-provided type whose TypeScript twin has no `accept`, and a page that called it emitted `node.accept(new Counter(), 0)` — a throw in the browser, measured before the fence. The fence follows the override from the call site's `OriginalDefinition`, since a constructed generic method overrides nothing. | the solution compiles; `ClosedHierarchyTests` (the closure, over both graphs); `UiFactoryConformanceTests` (factories are unaffected); the transpiled fixtures byte-identical without `EQ_UPDATE_TRANSPILED`; both samples build | S |
| S2 | **Landed ([#177](https://github.com/eQuantic/equantic-ui/issues/177)).** `Semantics.Walk` → `SemanticsVisitor`: 13 visits and 27 declines, not the 26 this row predicted — the vocabulary grew to 40 while the plan sat, which is exactly the arithmetic the visitor exists to stop anyone from doing by hand. A decline is a `const bool` named for its reason (`PureLayout`, `Wraps`, `Decorative`, `EscapeHatch`, `AwaitsGroupRole`, `Seam`) and the result IS the recursion: `true` keeps walking, `false` means this node announced and its subtree is part of what it says. The façade keeps the descent, because a frame's root and overlays are its business and not the vocabulary's. `WebFrame` gained a reason of its own — the pin had it filed under "pure layout", and it is the DOM escape hatch, which cannot cross at all. `Navigable` and `Overlay` decline through `AwaitsGroupRole`, which names [#187](https://github.com/eQuantic/equantic-ui/issues/187): the day a group role exists they become visits, and the constant is where a reader finds that out. The `Semantics` dispatch left the coverage pin. | `SemanticsTests`, `CheckSemanticsTests`, `HeadingSemanticsTests`, `GraphicSemanticsTests`, `LabelledNodesReachSemanticsTests`, `UnlabelledGroupSemanticsTests`, the three bridges' tests — 1,246 green, unchanged | S |
| S3 | **Landed ([#178](https://github.com/eQuantic/equantic-ui/issues/178)).** `EmailRealizer.Write` → `EmailVisitor` and `EmailRenderer.WalkText` → `EmailTextVisitor`: 6 visits each plus the seam, one shared refusal set of 33 behind one `Refuse` on the abstract `EmailWalk` both derive from — the seven the medium carries are abstract there, so neither alternative can answer for a node the other refuses. `WalkText` had no default arm, so it gained a refusal it never had; through `EmailRenderer` that can never fire (the HTML part is built first and throws), which is precisely why it needed a test rather than a whole-message assertion. Both dispatches left the pin. THE FAMILY SPLIT APPLIES TO `EmailWalk` AND NOT TO THE TWO VISITORS: the rule above exists to break up a 40-arm dispatch, and `EmailWalk` is one — the concrete visitors carry seven arms each and splitting them would be four files of ceremony. | `eQuantic.UI.Email.Tests` — 48 green and byte-identical output, plus `EmailRefusalParityTests`, which sends each of the 33 through both alternatives and expects the same reason from each (the vocabulary asked of the assembly, never listed); `InternalsVisibleTo` so the suite can reach a visitor the public API does not expose | S |
| S7 | `NodeKindTsGenerator`, `node-kinds.generated.ts`, `nodeKind: NodeKind`, `assertNever`. And a DECISION, taken on purpose rather than shipped by default: whether a page ever walks a tree. `Accept` is host-only since S1, so today no transpiled code visits; saying yes here costs `accept` on all 39 twin classes and a TypeScript `IVisualNodeVisitor`, and is taken only when a page has a reason. The TypeScript dispatch leaves the pin; `EveryNode_DeclaresItsOwnWireKind` becomes the generator's duplicate check. | `EnumUnionsTsGeneratorTests`' sibling; `tsc` over the runtime, which is what makes `assertNever` bite — `dotnet build src/eQuantic.UI.Runtime -t:TestRuntime` runs it and then `vitest run`, and vitest alone type-checks nothing; the transpiled fixtures byte-pinned | S |
| S4 | `WebRealizer.LowerNodeKind` → `WebLoweringVisitor`, four partial files. The dispatch leaves the pin. | `ComponentParityFixtureTests`, `PrimitiveValueFixtureTests`, `MarkerParityTests`, the SSR suites, and #121's `SurfaceSsrTests` | M |
| — | Audit step 4 first: hoist the five layout questions onto the vocabulary, so S5's arms shrink. | `FlexLayoutTests`, `LayoutCompositeTests`, the goldens | M |
| S5 | `LayoutEngine.MeasureCore` → `MeasureVisitor` with `MeasureState`. The dispatch leaves the pin. | `FlexLayoutTests`, `LayoutCompositeTests`, `FlexBasisWrapLayoutTests`, 91 goldens, `PerfHarnessTests` | M |
| S6 | `PhotonRealizer.EmitNode` → `EmitVisitor` over `laidOut.Source`; the nine `is` branches after the switch become visits; the two pre-switch guards stay as pre-visit logic on the class. The last dispatch leaves the pin. | `AbstractNodeGoldenTests`, `GoldenSceneTests`, `TreeGpuParityTests`, `BarChartPhotonTests`, `PerfHarnessTests` | M |
| S8 | `VocabularyCoverageTests` is deleted; the audit's section 2 says what holds the line now: the compiler. | — | S |

S7 is placed early on purpose: it is independent of the C# slices, and it is where the browser's
door gets the guarantee first.

## Open questions

1. **Overloads or `VisitBox`?** The plan says overloads; the alternative is spelled out above and costs
   a name per node. Recommend overloads.
2. **Public or internal?** Public. `Email` is a third realizer outside the native track, and the IDE
   consumer has a design host that realizes the vocabulary for its own canvas; both are visitors in
   waiting.
3. **Keep the regex pin as a second opinion after S8?** No. Once the compiler enforces the property,
   a regex that enforces it again is a second copy of a fact, which is what the audit exists to remove.
