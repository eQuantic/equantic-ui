# Flutter, one row at a time

Flutter is the reference this SDK was inspired by, so "how does Flutter solve this?" is the first
question to ask when a design decision is open. This is the answer to that question across
Flutter's whole surface, measured against ours rather than remembered.

**It is not a to-do list, and parity is not the goal.** Three verdicts appear here, and the third is
the one that matters:

| | |
|---|---|
| **SAME** | we arrived at the same shape, usually for the same reason |
| **DIFFERENT** | we answer the need another way, deliberately, and the entry says what we traded |
| **GAP** | Flutter answers something we do not, and nothing here is a decision — just an absence |

A DIFFERENT row is not a smaller version of Flutter's answer. Several of them exist because this SDK
has a constraint Flutter does not: **a component is written once and realized on a DOM and on a GPU
display list**, so anything that needs one target's machinery cannot enter the vocabulary at all.

Its companion is [ARCHITECTURE-AUDIT.md](ARCHITECTURE-AUDIT.md), which asks the other half of the
same question: this file asks *how does Flutter solve this?*, that one asks *where is our own
structure weak?*. Both are measured rather than recalled, and both are pinned.

Measured against the tree at the time of writing; every claim below was grepped, not recalled — and
kept true by `FlutterParityPinTests`, which reads this file and probes all 58 rows. A SAME,
DIFFERENT or PARTIAL row must be findable in the public surface; a GAP row must still be missing;
and a row added without a probe fails the build. So the audit cannot rot, cannot gain unchecked
prose, and cannot go on claiming an absence that has ended.

---

## 1. Widgets and the internal trees

| Flutter | Here | Verdict |
|---|---|---|
| `Widget` — immutable configuration | `VisualNode` (a COMPONENT here; "widget" is Flutter's word and stays in this column) | **SAME**, including the part people find surprising: it has **no `Parent`**, deliberately. It is rebuilt constantly, so a back-reference on it would mean nothing. |
| `StatelessWidget` | `StatelessComponent` | **SAME** |
| `StatefulWidget` + `State` | `StatefulComponent` with `SetState` | **SAME** in shape. One class rather than two: Flutter splits them because a `Widget` must be immutable and `State` must persist, and our reconciler keeps the retained instance instead. |
| `initState` | `OnMount` | **SAME**, with a narrower promise. It does **not** mean "the pixels exist" — Photon has no DOM to read geometry from, so a hook promising that could not be write-once. |
| `dispose` | `OnUnmount` | **SAME** — and it ships with `OnMount` rather than after it, because without the pair every mount is a leak. |
| `didUpdateWidget` | `AdoptConfig(UiComponent next)` | **SAME** |
| `didChangeDependencies` | — | **GAP**, and it follows from the Element row below: with no dependency graph there is nothing to be notified about. |
| `InheritedWidget` / `InheritedModel` | `GetService<T>()` over a `CapabilityScope` | **DIFFERENT.** Ours is AMBIENT, not positional: resolution walks a scope, not the tree. The trade is real and worth stating — no per-position override (two subtrees cannot see different values of the same thing) and no dependency-driven rebuild (changing a value does not invalidate the components that read it). What it buys is that a component never has to be handed a context to read the theme. |
| `Element` tree, `BuildContext` | — | **GAP, and the structural one.** There is no persistent instance tree, which is why `ComponentContext` is a bag of values (`Theme`, `TypeScale`, `Route`, `Density`) rather than a position. `Theme.of(context)` works in Flutter because the context IS the element and the lookup walks UP. Ours is handed the theme because there is nothing to walk. Everything in the row above, and the two rows below it, traces here. |
| `RenderObject` with `parent` | `LayoutNode` with `Parent` | **SAME.** Flutter makes this tree bidirectional and its configuration tree not; so do we, for the same reason. |
| `parentData` | — | **DIFFERENT, and needs nothing.** It exists to carry what a parent assigned — an offset, a flex factor — and this engine resolves all of that into `Bounds` in the same pass. A second slot would hold a copy. |
| `RenderSliver`, viewport virtualization | `ListView` (builds only the visible window plus overscan) | **PARTIAL.** The capability is there; the protocol is not. v1 fence, stated in the code: vertical only, fixed `ItemExtent`. There is no general sliver contract other nodes can implement. |

---

## 2. Layout, constraints and rendering

| Flutter | Here | Verdict |
|---|---|---|
| "Constraints go down, sizes go up, parents set positions" | The same discipline, single pass | **SAME** in behaviour. |
| `BoxConstraints` as a VALUE (`tight`, `loose`, `bounded`, `unbounded`) | Flags on `LayoutContext` — `IndeterminateWidth`, `StretchWidth`, `WindowWidth` | **DIFFERENT, and weaker.** The constraint is real but it is not a type a caller can hold, pass or reason about. That is why the next row is a gap rather than a small feature. |
| `LayoutBuilder` | `AdaptiveNode` (three window size classes) | **GAP.** We have the window-class special case of it, not the general node. The first external consumer of this SDK needed a child built against its own box, found no way to ask, and used a `Canvas` whose paint callback does nothing as a ruler — `ICanvasPainter` exposes `Width`/`Height`, so a handler-less canvas is a measuring tape. It works and re-measures across a resize. It is also an idiom nobody would guess, costing a node per use and a no-op draw callback per frame. |
| `CustomMultiChildLayout`, `MultiChildLayoutDelegate` | — | **GAP** |
| `CustomSingleChildLayout` | — | **GAP** |
| Subclassing `RenderBox` | — | **DIFFERENT by design.** The layout engine is closed; the vocabulary is the extension point. A consumer composes nodes rather than implementing `performLayout`, which is what keeps one component correct on both realizers. |
| `CustomPaint` / `CustomPainter` | `Canvas` + `ICanvasPainter` | **SAME**, and the painter already carries the box it was given. |
| `Canvas`, `Paint`, `Path`, shaders | The Photon engine's SDF shaders (Slang → Metal/SPIR-V) | **DIFFERENT.** Shaders are the ENGINE's, not an API. A consumer gets `Canvas` primitives; there is no `FragmentProgram`. Deliberate: a consumer shader would have to exist twice and match. |

---

## 3. State

| Flutter | Here | Verdict |
|---|---|---|
| `setState` | `SetState` + `StateInvalidated` | **SAME** |
| `ValueNotifier` / `ValueListenableBuilder` | — | **GAP** |
| `ChangeNotifier` / `ListenableBuilder` | — | **GAP** |
| `InheritedNotifier` | — | **GAP** (see the Element row) |
| `FutureBuilder` / `StreamBuilder` | `[ServerAction]`, `IServerPrefetch` | **DIFFERENT, and the disruptive one.** Flutter awaits in the tree and renders a spinner arm. We fetch on the SERVER before the page exists and hydrate the answer, so the first paint already has data and there is no loading arm to design. It is a better default and a real limitation: a stream that arrives while the user watches has no vocabulary here. |

---

## 4. Animation

| Flutter | Here | Verdict |
|---|---|---|
| `AnimationController` + `TickerProvider` | `IFrameTicker`, `LoopMotion`, `Presence`, `TransitionStore` | **DIFFERENT, and this is the deepest divergence.** Flutter's animation is IMPERATIVE — you hold a controller, drive it, dispose it. Ours is DECLARATIVE: a style diff plus a motion token, and the host interpolates. A component says what it looks like in each state, never how to get there. That is why there is no controller to leak and no `dispose` to forget. |
| `Tween`, `ColorTween`, `Matrix4Tween` | — (the engine interpolates) | **DIFFERENT** — the consequence of the row above. Nothing holds a tween because nothing drives one. |
| `Curves` | `Curve` enum + `Motion.Fast/Base/Slow` | **SAME**, on a fixed ladder: 100/200/300 and nothing between the rungs. |
| `AnimatedContainer` and the implicit family | `TransitionSpec?` — a `Transition` on the style, on `Text` and on the pressed/hover diffs | **SAME idea, smaller surface.** A property on the things that change rather than a parallel type per animatable property. |
| `AnimatedBuilder` / `AnimatedWidget` | — | **GAP** (imperative-only concepts) |
| `Hero` / shared element | — | **GAP** |
| `SpringSimulation`, `FrictionSimulation` | `SpringSpec` (stiffness, damping, mass) | **PARTIAL** — the spring is specified and used by gesture release; the other simulations are absent. |

---

## 5. Gestures, pointer and focus

| Flutter | Here | Verdict |
|---|---|---|
| `Listener` — raw pointer events | `Canvas.OnPointerDown/Move/Up` | **PARTIAL** — raw pointer exists only on the drawing surface, not as a wrapper over any subtree. |
| `HitTestBehavior` | Hit regions (Photon) / `pointer-events` (web) | **PARTIAL** — the behaviour is realizer-side, not an authorable enum. |
| `GestureArena`, competing recognizers | Host routing with a slop rule | **DIFFERENT, and thinner.** There is no arena: press, drag and scroll are resolved by the host in a fixed order. Simpler, and it cannot express two recognizers negotiating. |
| `GestureDetector` | `Pressable`, `Draggable`, `DragDismiss`, `Hoverable`, `Adjustable`, `Navigable` | **DIFFERENT, and better for this SDK.** Flutter has one detector with twenty callbacks; we have a node per INTENT, which is what lets each one carry its own semantics — a `Pressable` states its own accessible name and selection, a `Draggable` its axis and limits. |
| `RawGestureDetector`, custom recognizers | — | **GAP** |
| `FocusNode`, `FocusScope`, `FocusManager` | `InitialFocus`, the focus route, `Navigable` | **PARTIAL** — focus order and initial focus are expressible; there is no focus object to hold or move imperatively. |
| `Shortcuts` / `Actions` | `Shortcut` + `KeyChord` | **SAME** |

---

## 6. Routing

| Flutter | Here | Verdict |
|---|---|---|
| `Navigator.push/pop`, named routes | `Navigator` | **SAME** |
| `onGenerateRoute`, `RouteFactory` | `[Page("/route")]` | **DIFFERENT, and more disruptive.** A route is an ATTRIBUTE on the component, discovered at build time — there is no route table to keep in sync with the pages, and no factory to write. |
| Navigator 2.0 — `RouterDelegate`, `RouteInformationParser` | `context.Route` (params, query) + the router | **DIFFERENT.** The declarative half people want from Navigator 2.0 is what the attribute already gives; the imperative-history API is not reproduced. |
| Custom transitions (`PageRouteBuilder`) | `Presence` / motion tokens | **PARTIAL** |

---

## 7. Platform interop

| Flutter | Here | Verdict |
|---|---|---|
| `MethodChannel`, `EventChannel`, `BasicMessageChannel` | Typed capability interfaces — `ICamera`, `IBiometrics`, `ILocation`, `INetworkStatus`, `IPhotoLibrary`, `ISecretStore`, `IDeepLinks`, `IAppStorage`, `IClock`, `IMotionSensor`, `IFileDialogs`, `IWorkspace`, `IAnalytics`, `IConsent`, `IFrameTicker`, `IUiDispatcher` — sixteen — resolved with `GetService<T>()` | **DIFFERENT, and this is the product principle in one row.** No channel, no codec, no string method names, no argument maps. The developer writes C# against an interface and each shell implements it. A capability that is absent answers null rather than throwing at a serialization boundary. |
| `dart:ffi` | `LibraryImport`/`DllImport` — 25 files | **DIFFERENT.** P/Invoke is how the SDK talks to CoreText, DirectWrite, Vulkan and the rest. It is deliberately **not** a consumer surface: an app reaching for FFI is an app writing platform code, which this SDK exists to remove. |
| `AndroidView` / `UiKitView` | `WebFrame` | **PARTIAL** — one embedded platform view, not a general mechanism. |

---

## 8. Concurrency

| Flutter | Here | Verdict |
|---|---|---|
| `Isolate.spawn`, `SendPort`/`ReceivePort` | — | **GAP as vocabulary.** .NET brings `Task` and real threads, so the primitive exists; what does not exist is any UI-level story for handing work off and getting a rebuild back. |
| `compute()` | — | **GAP** |
| Marshalling back to the UI thread | `IUiDispatcher` | **SAME** |

---

## 9. Accessibility, i18n and platform adaptation

| Flutter | Here | Verdict |
|---|---|---|
| `Semantics` tree for screen readers | Our own semantics walk + the macOS/iOS/Android bridges | **SAME.** Worth recording how thin the ice was: six of the fourteen labelled nodes reached no bridge at all until the walk was enumerated by reflection rather than maintained by hand. |
| `MergeSemantics`, `ExcludeSemantics` | — | **GAP** |
| `Localizations`, `LocalizationsDelegate`, `Intl` | `.resx`, `CultureInfo`, `ICultureController`, culture routes | **DIFFERENT, and deliberately .NET's.** Localization is done the way .NET does it, not the way Flutter does it — a .NET developer already knows this API. |
| `TextDirection.ltr/rtl` | — | **GAP** |
| `ThemeData` vs `CupertinoThemeData` | One `IAppTheme` + `Density` read from the TARGET | **DIFFERENT, and better.** Flutter asks the author to pick a design language per platform and then reconcile two theme objects. We have one theme, and the density that makes a Mac toolbar tighter than a phone's comes from the target, never from the call site. |
| `MediaQuery` insets, notch, `devicePixelRatio` | `SafeArea`, `WindowChrome`, window size classes | **PARTIAL** — insets and size classes are expressible; there is no single query object. |

---

## 10. Application lifecycle and windows

| Flutter | Here | Verdict |
|---|---|---|
| `WidgetsBindingObserver` (`resumed`, `paused`, `detached`, `hidden`) | — | **GAP.** The shells know these states; nothing surfaces them to a component. |
| `View` / `PlatformDispatcher`, multiple windows | `WindowChrome` and the desktop shells | **PARTIAL** |
| `devicePixelRatio` | Render scale, realizer-side | **PARTIAL** — used, not authorable. |

---

## What this adds up to

**One gap explains several others.** There is no Element tree, so there is no position to look up from,
so there is no `InheritedWidget`, no `didChangeDependencies`, and no dependency-driven rebuild. That is
one decision to take, not four.

**Two gaps have a consumer already blocked behind them**, which is the only evidence that counts here:
`LayoutBuilder` (a child built against its own box) and an authorable constraint type to go with it.

**Three DIFFERENT rows are the ones to keep and defend**, because they are where this SDK is not a
smaller Flutter: declarative animation with no controller to leak; typed capabilities instead of
method channels; and a route that is an attribute rather than a table. Each of them is a direct
consequence of the product principle — the developer writes C# and never learns a platform artifact.
