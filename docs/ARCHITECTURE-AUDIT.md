# One vocabulary, six doors, three copies

The companion to [FLUTTER-PARITY.md](FLUTTER-PARITY.md), asking the other half of the same question.
That file asks *how does Flutter solve this?* and answers across Flutter's whole surface. This one
asks *where is our own structure weak?* — assembly by assembly, dispatch by dispatch — and answers
from counts taken off the tree on 2026-09-14, at `0.2.0-preview.52`.

Both are architecture, both are measured rather than recalled, and both are pinned so they cannot
rot. What holds each claim here:

| Claim | Instrument |
|---|---|
| every node reaches every dispatch, or is named there as an absence | `VocabularyCoverageTests` — six dispatches, C# and TypeScript |
| the layering — which core assembly may reference which, exactly | `AssemblyLayeringTests` — thirteen assemblies |
| the handoff speaks the vocabulary's current names | `HandoffVocabularyTests` |
| the handoff's numbers are the SDK's numbers | `HandoffTokenPinTests` |
| every Flutter row is findable, and every gap still a gap | `FlutterParityPinTests` — 64 rows |

The counts that carry no pin — lines, fields, how many times a word appears — are dated by the line
above and will drift. They are here to SIZE a decision, not to be believed a year on.

---

## 1. The layering, as it is

| Assembly | Lines | Files | Public types | May reference |
|---|---|---|---|---|
| `Primitives` — the vocabulary, the tokens, the contracts | 12,547 | 91 | 253 | nothing |
| `Components` — the write-once library | 9,593 | 61 | 99 | Primitives |
| `Charts` — the write-once charts | 839 | 5 | 12 | Primitives, Components |
| `Web` — the SSR realizer and the DOM escape hatch | 6,449 | 22 | 39 | Primitives |
| `Server` — the ASP.NET Core host | 4,848 | 33 | 51 | Web |
| `Email` — the third realizer | 606 | 2 | 3 | Primitives |
| `Native.Engine` (+ `Metal` 711, `Vulkan` 2,795, `Reference` 379) | 2,181 | 12 | 36 | Primitives |
| `Native.Framework` — layout | 2,803 | 11 | 25 | Primitives, Engine |
| `Native.Components` — the Photon realizer, semantics, the host | 4,707 | 12 | 27 | Primitives, Engine, Framework |
| `Native.Hosting` — the native application builder | 998 | 12 | 14 | Primitives, Native.Components |
| `Compiler` — eqc's library | 24,619 | 198 | 256 | Codegen |
| the TypeScript runtime | 35,540 (+13,658 of spec) | | | |
| the five shells | Windows 4,539 · Apple 2,365 · Android 1,832 · macOS 1,691 · iOS 836 | | | |

Read against Flutter's layers — `dart:ui` → `foundation` → `rendering` → `widgets` → `material` —
the mapping is direct for four of them and inverted for one, deliberately. `Native.Engine` is the
engine; the shells are the embedders; `Components` and `Material` are `widgets` and `material`;
`Native.Framework` plus the realizers are `rendering`. The inversion is the vocabulary: Flutter's
widgets sit ABOVE rendering, and ours — the 39 node types — sit BELOW every realizer, because a node
is written once and realized three times, so the realizers must be able to see it and it must not be
able to see them. That is the write-once architecture, and it is also why `Primitives` carries 253
public types, as many as the transpiler: it is `dart:ui`, `foundation` and the vocabulary in one
assembly. Section 4 weighs what else it carries.

**The rule the table protects: a realizer never references the library it realizes.** A component
reaches a realizer as the tree its `Build` produced, so `Web`, `Email` and `Native.Components` have
no reason to know `Components` — and one of them did. `Web` had referenced `Components` since the
merge that renamed the library (e19fb9fc, 2026-07-05), with no type from it used anywhere in the
assembly. The wiki's table said `Web` depends on Core and Primitives, the plan document said Core
and Components, the csproj said Primitives and Components, and nothing compared the three.

Removed here, and the proof is the arm a source-tree build cannot see. CLAUDE.md warns that a
reference is often a DELIVERY VEHICLE rather than a code dependency, so the whole product was packed
to a local feed at `1.0.0-dev`, the cache cleared of that version, a fresh app generated from the
template and restored from that feed alone, and built: `wwwroot/_equantic/HomePage.js`, `runtime.js`,
`equantic.css`, `strings/` — and the `Web` package's dependency list naming `Primitives` and nothing
else. The vehicle that delivers `Components` to eqc was never this edge: it is the consumer's own
`PackageReference` with `GeneratePathProperty` in `Sdk.props`, which a transitive dependency cannot
produce. `AssemblyLayeringTests` now holds the table above as exact sets, so the next edge added in
passing fails a test and owes a sentence here.

One coupling worth a line rather than a row: `Native.Build`, the icon tool, references BOTH the Apple
and the Windows shell — for CoreText and DirectWrite, so a C# icon's lettering is real glyphs on
whichever machine builds it — and with them every camera, location and biometry capability the
shells carry. A shell split into its text services and its capabilities would let a build tool take
the half it uses.

---

## 2. One vocabulary, six doors

The SDK has ONE visual vocabulary — 39 public node types in `Primitives` — and six places that decide
what each word means. Each is a switch over the node type. Each covers a different subset. Until the
pin, nothing made them agree, and **a deliberate omission and a forgotten one looked identical** to a
reader and to the build.

| Dispatch | Assembly | Method | Covers | On an unknown node | Decides |
|---|---|---|---|---|---|
| `LayoutEngine` | Native.Framework | `MeasureCore` | 37 / 39 | a zero-sized box, silently | how big it is, and where |
| `WebRealizer` | Web | `LowerNodeKind` | 38 / 39 | `null`, silently | what DOM the server writes |
| `lowering.ts` | TypeScript runtime | `lowerNodeKind` | 39 / 39 | `render()` or `null`, silently | what DOM the browser writes |
| `PhotonRealizer` | Native.Components | `EmitNode` | 28 / 39 | nothing, silently | what the GPU draws |
| `Semantics` | Native.Components | `Walk` | 13 / 39 | walks the children | what a screen reader says |
| `EmailRealizer` | Email | `Write` | 6 / 39 | **throws**, naming the node | what an email client may see |

Beside the six, at least ten smaller switches re-ask "what kind of node is this" from the consumer's
side: five inside `LayoutEngine` alone (`MinContentWidth`, `Shrinkable`, `WidthKind`,
`CrossSizeKind`, `PositionedOf`), four inside `WebRealizer` (`TextContentOf`,
`ResolveForPositioning`, `CapsAt`, `Fills`), and `EmailRenderer.WalkText`. Section 3 says what those
are really asking.

Low coverage is not itself a fault. `PhotonRealizer` paints LAID-OUT nodes, so a container the layout
pass already resolved into geometry has nothing left to draw; `Semantics` announces controls and
content, not rows. The fault was that this reasoning lived in nobody's head and in no test — and that
**five of the six default arms are silent.** The one loud arm, the email realizer's
`NotSupportedException` naming the node, belongs to the one dispatch of the six that has never
produced a defect of this family.

### What it cost

Seven defects, one cause. Four were found by someone looking at a running application, and the
seventh by the pin the day it grew to read the web realizer.

| Node | What happened | Found by |
|---|---|---|
| `Canvas.Label` | A labelled grip was silent to VoiceOver | a consumer, counting a11y elements |
| `Image.Label` | Emitted nothing on Photon while the web carried `alt` | an audit |
| `Vector` · `Drawing` · `CameraPreview` | Three more of the same, in one sweep | asking the assembly |
| `Text.Align` | Honoured by the web, dropped by all three native text services | a cross-pin (#103) |
| `VisualNode.Key` | Documented as reconciler identity, read by neither realizer | an audit (#95) |
| `Navigable` · `Overlay` | Open: honoured by the web, silent on Photon | still open |
| `SheetSurface` | **Closed.** The server rendered an EMPTY `<span>` where the browser draws a spreadsheet — no case in `LowerNodeKind`, `_ => null`, from the day it shipped (d8be2bd6). `SurfaceSsrTests` keeps it; its A/B is the empty span itself | this pass — found by this pin, fixed in the same week |
| `CodeSurface` | **Open, for a different reason than it was found for.** The empty span is understood; what blocks the arm is that the client appends a CARET to every code surface, so a server tree with only the child is one element short and the reconciler records a failed adoption. The shape has to be settled — does the server render the controller's caret, or does the client stop appending during hydration? — and settling it needs a running page | this pass |

### The asymmetry it exposes, and how Flutter avoids it

CLAUDE.md names the transpiler as the bar for the rest of the SDK. The transpiler has this exact
problem — dispatch over an open set of constructs, extended constantly, two implementations that
must agree — and answers it with 142 strategy files, a registry, and coverage suites that enumerate
by reflection against baselines that may only shrink. The realizers had the same problem and none of
that machinery.

*How does Flutter solve it?* It does not dispatch at all. Every `RenderObject` carries its own
`performLayout`, `paint` and `hitTest`, as abstract members: a node without them does not compile.
The vocabulary cannot do that — the realizers live above it and it must not see them — so the .NET
shape of the SAME guarantee is the **Visitor**: `VisualNode.Accept<T>(IVisualNodeVisitor<T>)` with
one abstract `Visit` per node, and each realizer implementing the interface. Adding a node to the
vocabulary then breaks every realizer's BUILD until the node is handled or explicitly declined, in
code, with the reason where the exempt lists carry it today. On the TypeScript side the same
guarantee is a generated `NodeKind` union — the generator that already writes `enums.generated.ts`
from the assembly — and `default: assertNever(kind)` at the bottom of `lowerNodeKind`.

Two patterns for two shapes of problem, and the shape decides: **Strategy with a registry for the
transpiler's OPEN set** (C# syntax, extended by every language version), **Visitor for the
vocabulary's CLOSED set** (39 types we own). One pattern applied to both would be wrong for one of
them.

### What holds it until then

`VocabularyCoverageTests` asks the ASSEMBLY for the vocabulary and the SOURCE for each dispatch's
cases — the one method that answers the question, cut out by its braces with comments and strings
removed, so a node named in prose or in a diagnostic message cannot pass for an arm. A node with no
case must be named in that dispatch's exemption list with its reason, and the list is checked in both
directions: a node with neither a case nor an exemption fails, naming it; an exemption for a node that
IS handled fails, so the list cannot become a record of what somebody once believed; an exemption for
a node the vocabulary no longer has fails from the far end. The TypeScript door is keyed by the wire
kind each C# node declares, and a fourth assertion holds those kinds unique — two nodes on one kind
would let the browser's pin pass for a node it has never heard of.

The first version of the matcher read the whole file, so an arm in `MinContentWidth` satisfied a claim
about `MeasureCore`, and an `Overlay` the engine does size sat in an exemption list saying it never got
there. Review caught both. The pin is the instrument, not the fix; when the last switch is a visitor,
it retires.

---

## 3. The vocabulary's shape

Thirty-nine concrete nodes, in four shapes the vocabulary never names:

| Shape | Count | Nodes |
|---|---|---|
| wraps exactly one child | 21 | `Box`, `Pressable`, `Link`, `Adjustable`, `Shortcut`, `Hoverable`, `Simulated`, `InView`, `InFlow`, `Presence`, `Draggable`, `DragDismiss`, `LoopMotion`, `Flexible`, `Positioned`, `Pinned`, `SafeArea`, `ScrollView`, `Overlay`, `CodeSurface`, `SheetSurface` |
| holds many | 4 | `Row`, `Column`, `Grid`, `Stack` |
| named slots | 3 | `Anchored` (anchor, panel), `AdaptiveNode` (compact, medium, expanded), `Navigable` (rows) |
| leaf | 11 | `Text`, `TextEntry`, `Icon`, `Image`, `Vector`, `Drawing`, `Canvas`, `Spinner`, `CameraPreview`, `WebFrame`, `Spacer` |

`FlexNode` — the base of `Row` and `Column` — is the only shape with a type. The single-child shape
is written 21 times, each node declaring its own `Child`, and so "a layout-transparent wrapper" is a
LIST held by the code that consumes it: enumerated 8 of 8 in `WebRealizer`, 6 of 8 in `LayoutEngine`.

*How does Flutter solve it?* Four abstract shapes, named once: `LeafRenderObjectWidget`,
`SingleChildRenderObjectWidget`, `MultiChildRenderObjectWidget`, and `ProxyWidget` for the wrappers
that change nothing about layout. Every widget picks one, and the framework walks children through
the shape rather than through the widget. Ours would be a `SingleChildNode` base (and the ten inner
switches of section 2 mostly disappear into it), with the same `IEnumerable<VisualNode>` the
multi-child nodes already implement.

**The questions the ten inner switches ask are questions about the NODE**, answered by the consumer:
does it shrink, what is its width kind, is it transparent to layout, does it cap at its content.
Flutter puts those on the object — `sizedByParent`, `isRepaintBoundary`, `alwaysNeedsCompositing` —
and the tree asks. Hoisting them onto the vocabulary is the same move as the base class: the realizer
asks instead of remembering, and two realizers cannot remember differently.

**58 public types in one file.** `Primitives/Nodes/VisualNode.cs` is 2,213 lines and declares 58
public types, while the same folder holds 23 other files. Readability rather than architecture — but
it interacts with section 2, because a vocabulary that is hard to enumerate by eye has dispatches
that are hard to check by eye, and the four shapes above are the lines to split it along.

---

## 4. What Primitives carries, and what it should

| Folder | Lines | Public types | What it is |
|---|---|---|---|
| `Nodes/` | 3,830 | 94 | the vocabulary — and nine files that are not nodes |
| `Code/` | 2,300 | 36 | a code editor's document, controller, history, six languages, completion, folds |
| `Theme/` | 1,590 | 35 | tokens, `IAppTheme`, `PhotonTheme`, palettes, the WCAG audit |
| `Devices/` | 1,408 | 39 | sixteen capability interfaces, and six `Photon*` declarations |
| `Vector/` | 1,365 | 13 | SVG parsing and vector drawings |
| `Sheet/` | 1,050 | 12 | a spreadsheet's document, controller, history, TSV codec |
| `Forms/` | 438 | 5 | form model and controller |
| `Contracts/`, `Text/`, `Layout/`, `Styles/`, root | 566 | 19 | attributes, culture seams, `EdgeInsets`/`SizeValue`, `ButtonStyles`, `Color` |

**Twenty-seven percent of the vocabulary assembly is two editors' models.** `Code/` and `Sheet/`
together are 3,350 lines and 48 public types, in the assembly whose stated contents are "abstract
visual vocabulary, tokens and the contract attributes". They are here because `CodeSurface` and
`SheetSurface` — nodes, which must be here — take a concrete `CodeEditorController` and
`SheetController`, and because both realizers drive the same editing protocol through them
(`CodeKeymap`, `SheetKeymap`) without being allowed to reference `Components`. *How does Flutter
solve it?* `TextEditingController` and `EditableText` live in `widgets`, not in `rendering`; the
widget owns the protocol and `RenderEditable` only paints. Ours could take the same road — the node
depends on an interface stating what a realizer READS (lines, caret, selection, tokens), the
controllers and their six languages move up — or the weight can be accepted with a reason written
down. What cannot stand is the current answer, which is neither. Edgar's call.

**Three more that belong a layer up or out**, each small:

- `Styles/ButtonStyles.cs` — a component's style resolver, used by `Button` and the TypeScript
  design-system generator. Flutter keeps `ButtonStyle` in `material`. → `Components`.
- `Theme/PaletteAudit.cs` — 346 lines of WCAG arithmetic that validates a `DataPalette`. Used by
  `DataPalette.Default` and by tests; shipped in every browser bundle and every AOT image. → tests,
  or an analyzer.
- the six `Photon*` types in `Devices/` — `PhotonCapabilityAttribute`, `PhotonEntitlementAttribute`,
  `PhotonBundleKeyAttribute`, `PhotonEntitlements`, `PhotonBundleValueKind` (and `PhotonTheme`, which
  is the design system's default and stays). The attributes are read by `Native.Build` and one shell
  and by nothing on the web; they are native-only declarations in the neutral assembly, put there so
  the source generator's output would resolve. `Native.Hosting` is referenced by every native app and
  is where they resolve just as well.

**And one thing missing from the bottom.** `Primitives` has `EdgeInsets`, `SizeValue`, `CornerRadii`
and no `Rect`, `Point` or `Size`: geometry lives in `Native.Engine/Geometry.cs`, ABOVE the vocabulary.
Flutter puts `Rect`, `Offset` and `Size` in `dart:ui`, under everything. The consequences are
measurable: `ICanvasPainter` spells every box as four floats; `Charts` carries its own `BarRect`
with x, y, width and height spelled out, since no `Rect` is visible to it; `SemanticNode` carries a `Rect` and therefore cannot move down to
`Primitives` — which is what keeps `SemanticRole` inside one target's assembly (section 6) and
`Navigable`/`Overlay` mute on Photon; and `LayoutConstraints` — the constraint value #119 introduced, the one an author-facing
`LayoutBuilder` would hand out, the gap in FLUTTER-PARITY with a consumer already blocked behind it —
had to be born in `Native.Framework`, above the vocabulary, for want of a lower home. Moving three record structs down breaks nothing above them.

**Folder hygiene, for the reader.** `Nodes/` holds nine files that are not nodes — `CapabilityScope`,
`ComponentBoundary`, `ComponentInstanceStore`, `RouteValues`, `Navigator`, `IServerPrefetch`,
`IHandleStatus`, `IAppIcon`, `WindowChrome` — while `Contracts/` exists for exactly that kind of
thing. And one target's word survives in a public signature: `Navigator.Go(string href)`, in the
assembly whose rule is that no name would exist if the web did not. `destination`, like the `Link` it
is the imperative twin of.

---

## 5. The Element tree, as a string

FLUTTER-PARITY names the structural gap: Flutter has three trees — `Widget` (configuration), `Element`
(the persistent instance, which IS the `BuildContext`), `RenderObject` (layout and paint) — and we have
the first (`VisualNode`) and the third (`LayoutNode`) and not the second. This section measures what
stands in for it.

**Identity is a string.** A retained component is found by `"{path}#{type}#{key}"`; a transition
track by `path + ":elev.y"`; a scroll offset, a presence, a drag, an in-view state, a focus, a caret
and a hover by the path of the node they belong to. Six stores keyed by path (`ComponentInstanceStore`,
`TransitionStore`, `ScrollStore`, `PresenceStore`, `DragStore`, `InViewStore` — eleven dictionaries),
five more path fields on `PhotonHost`, a `PathCache` on both `LayoutContext` and the host to make the
strings cheap, and a `LayoutNodePool` to make the tree cheap. Paths are built or parsed at 72 sites:
24 in `LayoutEngine`, 31 in `lowering.ts`, 8 in `TransitionStore`, 5 in `PhotonHost`, 4 in
`PhotonRealizer`. Flutter has none of this: the `Element` IS the position, state hangs off it, and a
`GlobalKey` is the exception rather than the addressing scheme. The Element tree exists here — spread
across six stores and keyed by string.

**Context is ambient.** With no position to look up from, everything Flutter finds by walking up the
`BuildContext` is a static here: thirteen `AsyncLocal` channels (seven in `Primitives` —
`CapabilityScope`, `RouteValues`, `InFlow`, `FaceResolution`, three on `ComponentBoundary` — and six
in `Web`) plus two process-wide statics, `IUiDispatcher.Current` and `Navigator.Handler`.
`AsyncLocal` is the right answer for SSR, where concurrent requests render on shared instances; it is
the wrong shape for two windows in one process, which `Navigator.Handler` — "one surface owns it" —
already admits.

**And at the web seam, two of everything.** `Web/Dom/RouteData` and `Primitives/RouteValues` answer
`Param` and `Query` identically; `RenderContext` carries its own `AsyncLocal` service provider,
route and link policy beside `CapabilityScope` and `RouteValues.Current`, and `ServerRenderingService`
arms both sets on every request. The Core dissolution (#83) moved the write-once half down and left
the web half in place.

This is ONE decision, as the parity audit already concludes: an instance tree gives per-position state
without string keys, positional lookup (`InheritedWidget`), dependency-driven rebuild and
`didChangeDependencies` in one move. It is also the largest move in this document, and the stores
above are the inventory of what it would absorb. The two-of-everything at the web seam does not wait
for it: `RouteData` → `RouteValues` and the provider → `CapabilityScope` are a morning's work each.

---

## 6. Hosts and realizers: what Flutter splits, we hold in one class

| File | Lines | Shape |
|---|---|---|
| `Runtime/src/shared/lowering.ts` | 3,418 | the browser's realizer, one module |
| `Web/WebRealizer.cs` | 2,629 | the server's realizer, one static class, ~70 `Lower*` methods |
| `Compiler/CodeGen/TypeScriptEmitter.cs` | 2,411 | 71 methods; the strangler boundary's last text |
| `Primitives/Nodes/VisualNode.cs` | 2,213 | 58 public types |
| `Design/DesignSession.cs` | 2,151 | the visual editor's session |
| `Native.Components/PhotonHost.cs` | 2,074 | 44 fields, 27 public methods |
| `Native.Framework/Layout/LayoutEngine.cs` | 1,948 | one static class, six switches |
| `Native.Components/PhotonRealizer.cs` | 1,805 | one static class |
| `Server/UIExtensions.cs` | 1,461 | five types; `ServeAppShell` alone is 348 lines |

Nine files, 20,110 lines — the nine largest hand-written files in the tree.

**`PhotonHost` is Flutter's seven bindings in one class.** `WidgetsFlutterBinding` mixes in
`GestureBinding`, `SchedulerBinding`, `ServicesBinding`, `PaintingBinding`, `SemanticsBinding`,
`RendererBinding` and `WidgetsBinding`, each a class with one concern. `PhotonHost` routes pointers,
schedules frames, owns focus, holds three caches, retains component instances, hot-reloads, AND
carries the editing protocol of three surfaces — caret, selection, word selection, IME composition,
sheet fill, code editing — 681 lines of it, a third of the file. The shells reach it through 27 public
methods and no interface: macOS drives 18 of them, Windows 14, Android 8, and nothing says which
subset a shell must implement. *How does Flutter solve it?* `FocusManager` owns focus; `TextInput`
bridges the platform keyboard to a `TextInputClient` the widget implements; the host binding routes.
The split is `FocusManager`, a `TextEditingSession` per surface kind, a `GestureRouter`, and an
`IPhotonHost` that IS the shell contract.

**The realizers are one class each, and section 2's Visitor is also their split.** The transpiler
shows the folder shape: `Strategies/Expressions/…`, one file per construct. A `Web/Lowering/` with one
visitor part per node family, and the same under `Native.Components/`, turns four monoliths into
folders a reviewer can hold — and the coverage becomes something the compiler checks.

**The server composes its shell by hand.** `UIExtensions.cs` holds `AddUI`, `MapUI`, `ServeAppShell`,
`UIOptions`, `HtmlShellOptions` and `ThemeCookie` in one file; `ServeAppShell` builds the document
over 348 lines with an inline `JsStr` escaper; and `HtmlTemplateEngine` is 404 lines of regex
templating (`{{#if}}`, `{{?x}}`) that renders exactly one template, `Templates/app-shell.html`. The
repo already has `Codegen` — one `CodeWriter`, one writer per file type — for precisely "a generated
document assembled from pieces". The ASP.NET idiom is the other half: options classes in their own
files, the endpoint as a class, the shell as a writer.

**Small duplications, ordinary rather than structural.** The desktop shells decide `Density.Compact`
in four places (runner and window, on both macOS and Windows) — one fact per target, written twice
per target. `WebRealizer` resolves `theme.Elevation(level)` six times in three places, twice each
(once to ask `IsNone`, once to format); `PhotonRealizer` twice.

---

## 7. The vocabulary exists three times

Flutter has one language, so a node exists once. Ours has two, and the SDK's stated answer to that is
eqc: the 139 component modules embedded in `runtime.js` are TRANSPILED from the C# in `Components`
and `Primitives` — `CodeEditorController`, `SheetController`, `FormController`, `MarkdownParser`
among them — and byte-pinned against the live compiler. The vocabulary itself does not take that road.

| C# | Hand-written TypeScript twin | Kept in step by |
|---|---|---|
| `VisualNode.cs` (2,213) + `LayoutTypes`, `CornerRadii` | `vocabulary.ts` (1,787) + `nodes.ts` (877) + `value-types.ts` (397) | `vocabulary-config.spec.ts`, `DesignSystemTsGenerator`, the transpiled fixtures |
| `WebRealizer.cs` (2,629) | `lowering.ts` (3,418) | cross-pinned style strings, `MarkerParityTests` |
| `StyleAtomizer.cs` (421) | `style-atomizer.ts` (468) | byte-identical hashes, both suites |
| `ComponentInstanceStore.cs` (90) | `instance-store.ts` (195) | the reconciler specs |
| `ComponentBoundary.cs` (167) | `component-boundary.ts` (162) | the boundary specs |

The realizer twin is legitimate: the browser lowers to live DOM with listeners and the server lowers
to markup, and the two must agree rule for rule — that is what the cross-pins are for. The other four
rows are twins of PURE C#: data classes, a hash, a dictionary keyed by path, a try/catch around a
build. `vocabulary-config.spec.ts` exists because a hand-written twin can forget the trailing config
parameter eqc emits and silently drop an initializer — a class of defect a generated twin cannot
have. The question to answer before the move is the honest one: what stops eqc from emitting
`vocabulary.ts` today (the self-lowering `render()` hook, init-only properties, the record structs)?
Whatever it is, it is a transpiler gap the shared library already crossed for 139 modules.

**One transpiled set is committed twice.** `shared/components/` (8,200 lines, 140 files) and
`shared/__transpiled__/` (8,126 lines, 142 files) are the same bytes except one import line
(`"@equantic/runtime"` versus `"../runtime-exports"`) plus three test-only fixtures, regenerated
together by `SharedComponentTranspilationTests`. The vitest config already aliases
`@equantic/runtime` to `src/index.ts`, and the embedded copy's import is a one-line `Replace` in the
test. One committed set and a rewrite at test time retires 8,000 generated lines from the repo.

**And the semantics exist three times too.** `SemanticRole` — what a node IS to assistive technology
— is declared in `Native.Components`, a target's assembly. The web decides the same things inline: 35
`aria-`/`role` decisions in `WebRealizer`, 86 in `lowering.ts`, and neither can name the enum;
`WebRealizer` cites it in a comment (line 1123) in a file its assembly cannot see. Three answers to
one question, and the one with a type is the one the other two cannot reference. The move is
`SemanticRole` and `SemanticNode` to `Primitives`, per-target bridges staying where they are — blocked
only by the `Rect` of section 4.

**And a truncated line ends four different ways.** `ITextMeasurer.Measure` promises text "truncated
to `maxLines` with a trailing ellipsis". The web draws one (`text-overflow: ellipsis`, and the
multi-line clamp). Android's measurer appends `…` itself. CoreText and DirectWrite cut the line and
draw nothing, and CoreText's class doc calls that a v1 fence — a decision about every target's text,
written in one target's file, where the neutral side never reads it. The one bit that would let the
realizer draw the mark uniformly for all three, `MeasuredLine.Ellipsized`, is written by every
measurer and read by no product code: its only readers are three tests, one of which asserts "the
shrunk text ellipsizes" against a measurer that does not. *How does Flutter solve it?* `TextPainter`
owns `ellipsis` and `maxLines` in the neutral `painting` layer; the platform shaper only measures.
Ours is the same move: the realizer appends the mark when `Ellipsized` says a line was cut, the three
measurers stop deciding, and the contract stops promising what one of them does. Same family as
`Text.Align` (#103) — the property one realizer honours and another drops in silence — found by the
IDE consumer's session and measured here.

---

## 8. The handoff and the code, in step

The design system lives in `docs/design/` and is corrected here, so alignment can be a TEST. Two
things are pinned today and one is not.

**Pinned — the numbers.** `HandoffTokenPinTests` walks every leaf of `tokens.json` and compares it
with the value the SDK returns: surfaces, ten type roles, spaces, radii, shape, icons, touch,
elevations, motion, control metrics in both densities.

**Pinned — the words, from this pass.** The vocabulary retired `Alt` (→ `Label`), `ZIndex` (→ `Layer`),
`Sticky` (→ `Pinned`) and `Href` (→ `Destination`) because a target's word does not belong in it, and
it never says "widget". Each rename moved the code and the comments and could not move the design
pages: the A11 Image block still read `new Image(…, alt: "…")` and "alt: required or explicit
decorative: true" ten days after `#81`, while the code has `Label`, with the empty string meaning
decorative. `HandoffVocabularyTests` scans every page, the token export, the exported C# view and the
notes for the retired spellings; a re-export from the design tool that brings one back fails naming
the file and the line. The device frames under `frames/` are excluded — a `zIndex` on a React bezel
is CSS, not our vocabulary.

**Not pinned — the component blocks.** [HANDOFF-FIDELITY-AUDIT.md](HANDOFF-FIDELITY-AUDIT.md) holds
294 claims from the handoff's 47 component blocks: 73 confirmed divergences, 5 refuted, 216 never
re-checked, 56 documented deviations. Seven have closed since 2026-08-16, each with a test naming its
block (`ListHandoffFidelityTests`, `NavigationHandoffFidelityTests`, `ToggleHandoffFidelityTests`,
`PointerContractFidelityTests`, `DestinationSemanticsTests`, `GraphicSemanticsTests`). The rest is
prose in HTML, which is why it is an audit that rots and not a pin that fails. The ask to the design
tool — corrections flow both ways — is the one `tokens.json` already answered: export the blocks'
checkable facts (metrics, states, roles per component) as data, and the fidelity audit becomes
`HandoffTokenPinTests`' sibling instead of a 3,675-line snapshot.

---

## 9. Garbage, and what preview permits

Preview is the moment to break, remake and delete; the rule is to leave nothing behind. What this pass
removed, and what it found for the next one:

**Removed here.**

- The `Web → Components` reference — dead for ten weeks, proven so in the package arm (section 1).
- The `Sdk.props` alias block's comment, which described `eQuantic.UI.Core` declaring the component
  bases beside `Primitives` and making them ambiguous. Core was dissolved in #83; only `Primitives`
  declares them. The two aliases stay because they DO something else: they put `StatefulComponent`
  and `StatelessComponent` in scope by name with no `using`, the same way the native SDK does.
- `alt: "…"` in the A11 Image block of the handoff.

**Found, each with evidence, for a decision.**

- **The compile-time class evaluator serves nothing.** `[CompileTimeEvaluate]` is applied to zero
  types in the tree; `CompileTimeEvaluator` (1,345 lines), its strategy (164), the attribute (65),
  `CssEmitter` (222), `StyleClass` (312), its tests (213) and four documents (1,561 lines —
  `COMPILE-TIME-EVALUATION-INDEX/-SUMMARY`, `COMPILER-COMPILE-TIME-EVALUATION`,
  `COMPILER-IMPLEMENTATION-GUIDE`) total some 4,100 lines around a Tailwind adapter the index itself
  says was removed. `ClassBuilder` (230) is different: CLAUDE.md names it as the DOM escape hatch's
  class utility, so it stays unless that changes. Everything else here is the adapter's shadow.
- **Two plans reference a roadmap that is not in `docs/`.** `IMPLEMENTATION-PLAN.md` and
  `PHASE-2-CLIENT-ROUTER-PLAN.md` (untouched since 2026-06-10, both marked complete in their own
  status lines) point at `ROADMAP.md`; the roadmap lives in the wiki. Finished plans are history, and
  history is git's.
- **`docs/design/Tokens.handoff.cs` is a third voice.** The README keeps it "for comparison"; no test
  reads it, it compiles into nothing, and the day the handoff moved here it disagreed with
  `tokens.json` beside it about the Link variant. `tokens.json` is pinned; this is not.
- **`Web/Dom/IComponent.cs`** carries a `<summary>` for ARIA attributes that summarises nothing and a
  duplicated `<summary>Child components</summary>` — prose rot in the escape hatch's root interface.
- **`Navigator.Go(string href)`** — section 4.
- **The wiki's Architecture page** still shows `Sdk="eQuantic.UI.Sdk/1.0.0"`, `net9.0`, a
  `manifest.json` step, "Code splitting", and `/_equantic/pages/Counter.js` — a pipeline CLAUDE.md's
  bundle strategy says does not exist (no `pages/` folder, no chunk splitting). The wiki is its own
  repository and its guards have a baseline; this is a pointer, not a fix.

---

## 10. Measured and healthy

Worth recording, because an audit that only lists faults misleads about the whole.

- **The transpiler is the bar it is said to be**: 142 strategy files behind one `IConversionStrategy`
  and one registry, an IR with one writer per level, and four baselines that may only shrink
  (`ir-migration`, `bcl-surface`, `diagnostics`, `conversion-gaps`).
- **The engine is a real RHI**: `IRenderBackend` / `IRhiDevice` / `IRhiCommandList` / `IRhiTexture`
  with three backends — Metal, Vulkan, Reference — behind them, and parity suites between them.
- **Platform interop is typed**: sixteen capability interfaces, resolved by `GetService<T>()`, absent
  as `null` rather than as a serialization error. FLUTTER-PARITY calls this the product principle in
  one row, and the measurement agrees: no channel, no codec, no method name as a string.
- **The realizers do not re-derive the design system**: variant colours, shape, type roles and
  disabled opacity resolve once, above all three. What was duplicated is the type DISPATCH (section 2),
  not the decisions.
- **The vocabulary's names keep its rule.** No public signature in `Primitives` carries a target's
  word, the four that did having been renamed — one parameter excepted (section 4). Its PROSE mentions
  the web freely, 73 times in `VisualNode.cs` alone, to say what a node lowers to; that is what the
  comments are for.
- **The declarative surface is held to its contract**: `UiFactoryConformanceTests` checks that each of
  the 75 factories in `UI.cs` is named like its type and mirrors a constructor parameter for
  parameter, with three named exceptions listed by name.
- **The pins exist, and they are the pattern**: `FlutterParityPinTests` (64 rows, a probe each),
  `HandoffTokenPinTests`, `VocabularyCoverageTests` (six doors), `AssemblyLayeringTests`,
  `HandoffVocabularyTests`, `MarkerParityTests`, `LabelledNodesReachSemanticsTests`, the transpiled
  fixtures byte-pinned against the live compiler, the design-system TypeScript byte-pinned against its
  generator. 2,678 xUnit facts and theories across ten projects, 830 vitest cases across 127 specs.
- **The handoff is clean of the retired words**, as of this pass, and will fail the build the day it
  is not.

---

## 11. Order of attack

Each step names the Flutter answer it follows and its size. The pins come first because they are what
makes the rest safe.

0. **Done in this pass.** The six-door coverage pin, the layering pin, the handoff vocabulary pin, six
   parity rows with probes, the dead `Web → Components` edge, the alias comment, the A11 word.
1. **The SSR surfaces defect** (`CodeSurface`, `SheetSurface` → empty span). ~~`SheetSurface`~~ done:
   the server writes the grid and its child, and its exemption is gone from the coverage pin — the
   first time that list has shrunk. `CodeSurface` remains, and the question is now a SHAPE one: the
   client appends a caret to every surface, so an arm that writes only the child hands hydration a
   tree one element short. Either the server renders the controller's caret (it has the state) or
   the client stops appending during hydration; the choice needs a running page to settle. — S done,
   S remaining
2. **Visitor over the vocabulary, generated `NodeKind` union with `assertNever` in TypeScript**
   (Flutter: abstract `performLayout`/`paint`). One file per node family per realizer, as
   `Strategies/` is per construct. Retire the regex pin when the last switch is gone. — L
3. **Geometry down**: `Rect`, `Point`, `Size` to `Primitives` (Flutter: `dart:ui`); then `SemanticRole`
   and `SemanticNode` to `Primitives`, then the group role that unmutes `Navigable` and `Overlay` on
   Photon; `Charts` drops `BarRect`'s own geometry; `ICanvasPainter` takes a `Rect`. — M
4. **Node shapes**: a `SingleChildNode` base (Flutter: `SingleChildRenderObjectWidget`), the wrapper
   set and the node-intrinsic questions hoisted onto the vocabulary, `VisualNode.cs` split along the
   four shapes. — M
5. **The Primitives diet**: `ButtonStyles` → `Components`; `PaletteAudit` → tests or an analyzer;
   `Photon*` attributes → `Native.Hosting`; `Navigator.Go(href)` → `destination`; `Nodes/` holds
   nodes. And Edgar's decision on the editor models (Flutter: controllers in `widgets`). — S, plus a
   decision
6. **Two of everything at the web seam**: `RouteData` → `RouteValues`, `RenderContext`'s provider →
   `CapabilityScope`, one link policy. — S
7. **The truncation mark**: the realizer draws `…` when `MeasuredLine.Ellipsized` says a line was cut
   (Flutter: `TextPainter.ellipsis`), the three measurers stop appending or fencing it, the contract
   says what happens, and the test that asserts an ellipsis asserts a real one. — S
8. **The adapter's shadow**: the compile-time evaluator, `CssEmitter`, `StyleClass`, the four
   documents; `Tokens.handoff.cs`; the two finished June plans; `IComponent.cs` prose. — S, after
   Edgar confirms `ClassBuilder` stays the escape hatch
9. **One transpiled set** in the runtime. — S
10. **Generate the vocabulary twins with eqc**: measure what stops it, close that, then `vocabulary.ts`,
   `value-types.ts`, `instance-store.ts` and `component-boundary.ts` become emitted. — L
11. **`PhotonHost` split** (Flutter: seven bindings): `FocusManager`, a `TextEditingSession` per
    surface kind, a `GestureRouter`, and `IPhotonHost` as the shell contract; the server's shell moves
    to a writer. — L
12. **The Element decision** — the one move that closes `InheritedWidget`, `didChangeDependencies`,
    dependency-driven rebuild and string identity together. A design document of its own, with
    section 5 as its inventory. — L, and a decision first

## Not yet measured

The compiler's internals beyond file sizes — the assembly already holding the bar; the shells' own
platform code beyond what they drive on the host; the design host's `DesignSession`; the Server's
endpoint surface; and the TypeScript runtime's `core/` and `dom/` beyond the twins named above.
