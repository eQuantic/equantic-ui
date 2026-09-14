# One door per node

> The plan for step 2 of [ARCHITECTURE-AUDIT.md](ARCHITECTURE-AUDIT.md): the dispatches over the
> vocabulary become one visitor per DISPATCH — one for each realizer, and two for email, whose HTML
> and plain-text halves are two dispatches today — so that a node added to the vocabulary is a COMPILE
> ERROR in every realizer until it is handled or declined, in code, with the reason beside it.
> Measured on 2026-09-14 against `main` after #120; the numbers are dated by that line.

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
| S1 | `IVisualNodeVisitor<,>`, `Nothing`, `Accept` on `VisualNode`, forty one-line overrides, and the `private protected` constructors on `VisualNode` and `FlexNode` that close the hierarchy. No consumer yet. | the solution compiles; `ClosedHierarchyTests` (the closure, over both graphs); `UiFactoryConformanceTests` (factories are unaffected) | S |
| S2 | `Semantics.Walk` → `SemanticsVisitor`: 13 visits, 26 declines named for their reason; `Navigable` and `Overlay` decline until the group role of audit step 3 lands, then become visits. The `Semantics` dispatch leaves the coverage pin. | `SemanticsTests`, `CheckSemanticsTests`, `HeadingSemanticsTests`, `GraphicSemanticsTests`, `LabelledNodesReachSemanticsTests`, `UnlabelledGroupSemanticsTests`, the three bridges' tests | S |
| S3 | `EmailRealizer.Write` → `EmailVisitor` and `EmailRenderer.WalkText` → `EmailTextVisitor`: 6 visits each, one shared refusal set of 33 behind one `Refuse`, which throws the `NotSupportedException` the HTML default arm throws today. `WalkText` has no default arm — a node it does not know falls out of its switch in silence — so the text visitor gains a refusal it never had, and that is the point: what one alternative refuses, the other refuses too. Both dispatches leave the pin (the second was never in it). | `eQuantic.UI.Email.Tests`, plus one fact that sends each of the 33 through both alternatives and expects the same refusal from each | S |
| S7 | `NodeKindTsGenerator`, `node-kinds.generated.ts`, `nodeKind: NodeKind`, `assertNever`. The TypeScript dispatch leaves the pin; `EveryNode_DeclaresItsOwnWireKind` becomes the generator's duplicate check. | `EnumUnionsTsGeneratorTests`' sibling; `tsc` over the runtime, which is what makes `assertNever` bite — `dotnet build src/eQuantic.UI.Runtime -t:TestRuntime` runs it and then `vitest run`, and vitest alone type-checks nothing; the transpiled fixtures byte-pinned | S |
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
