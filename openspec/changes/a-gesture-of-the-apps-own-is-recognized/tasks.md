## 1. The vocabulary

- [ ] 1.1 `GestureDisposition` (Accepted, Rejected; append-only, `EnumValueAbiTests`), `IGestureArenaMember`, and `GestureRecognizer` in `src/eQuantic.UI.Primitives/Gestures/`, with `IsPointerAllowed`, `AddAllowedPointer`, `HandleEvent`, `AcceptGesture`, `RejectGesture`, `Resolve`, `ResolvePointer`, `StopTrackingPointer`, `StartDeadline`, `DidExceedDeadline`, and the tracked pointers
- [ ] 1.2 `GestureRecognizerFactory` and the sealed `GestureRecognizerFactory<T>(Func<T> create, Action<T> initialize)`
- [ ] 1.3 `RawGestureDetector(VisualNode child, IReadOnlyList<GestureRecognizerFactory> gestures)` with `Behavior`, every registration a node has (visitor member, node kind, coverage suites), and `UI.RawGestureDetector`
- [ ] 1.4 Each new type in `primitives-types.fixture.json` and `PublicAPI.Unshipped.txt`; read the diff
- [ ] 1.6 `PointerEvent.TimeMs` (design 6b): the record widens, its unshipped entries move; the web reads the DOM event's `timeStamp`, Photon the shell's time or its own `Stopwatch`, the host's pointer entry points take an optional `timeMs`, the macOS shell passes `NSEvent.timestamp`; the value twin and its fixture carry it, and `Listener`'s tests on both targets assert it rises monotonically
- [ ] 1.5 Check against the real thing: a recognizer subclass written as an app writes it (fields, a nullable `Action<Point>`, `StartDeadline`, `Resolve`, an override of every virtual) transpiled by eqc in a compiler test, and its twin executed against the runtime's `GestureRecognizer` in vitest; a construct eqc refuses is filed and fixed in eqc, never written around (PRINCIPLES 14)

## 2. The arena, written once

- [ ] 2.1 `GestureArena` in Primitives, Flutter's `GestureArenaManager`, its entry's state one value (open, closed, swept) that a later HOLD adds to: `Add(pointer, member)` returning the entry that resolves it, `Close`, `Sweep`, the eager winner, a lone member winning at the close, the last member left winning, the first member winning at the sweep (no hold: design, Open Questions)
- [ ] 2.2 `GestureRouter` in Primitives: the per-host object a realizer drives (`Down` with the members in hit order, innermost first, then the arena closed; `Route` for move, up and cancel; an up sweeps, a cancel rejects every member; `Tick(nowMs)` and `NextDeadlineMs` for the recognizers' deadlines; the recognizer sets kept by position and initialized from the latest factories)
- [ ] 2.3 `gesture-arena.fixture.json` under `src/eQuantic.UI.Runtime/src/shared/`: sequences of downs, resolutions, ups, cancels and ticks with the outcome each member hears, which the C# suite runs against `GestureRouter` and vitest runs against its transpiled twin
- [ ] 2.4 Check against the real thing: the fixture green on both sides, and each case failing when the arena gives the sweep to the LAST member instead of the first (the plausible wrong rule)

## 3. Web

- [ ] 3.1 The detector's lowering on both producers (`WebLoweringVisitor.Interaction.cs` `LowerRawGestureDetector` and `lowering.ts` `lowerRawGestureDetector`): the listener's mark with its behaviour, a `data-eq-gestures` mark, and the event through which the controller asks the detector for its latest factories
- [ ] 3.2 `dom/gesture-arena.ts`: one router per document; on a down, the members in hit order (the detectors `listenerHits` returns, innermost first, each one's recognizers, and the built-ins under the point); events routed by `pointerId` in each detector's own box; deadlines on `setTimeout` from `NextDeadlineMs`; a non-passive `touchmove` that prevents the browser's pan only once a recognizer won, and a `selectstart` and a `contextmenu` prevented then too; the `click` after an up swallowed when the press lost
- [ ] 3.3 The built-ins join: the innermost `Pressable` under the down (wins only at the sweep; rejected, its click is swallowed); `followPointer` (Draggable, DragDismiss, Sortable) accepts when it arms and detaches when rejected; the context hold accepts when its menu is asked and stops when rejected; the table viewport's pan accepts past its slop; the browser's scroll is a member that accepts on `pointercancel`
- [ ] 3.4 Vitest over the controller: a recognizer that accepts cancels a draggable that had not armed and swallows the click of a button under it; a draggable that arms first rejects the recognizer; a `pointercancel` rejects every member
- [ ] 3.5 Measure in Chromium with real touch input (CDP `Input.dispatchTouchEvent` on a `hasTouch` context) and write the measured answer into the design and the spec: whether a touch that holds 500 ms and then travels is kept from scrolling by a `touchmove` prevented after the hold, and whether a touch that travels at once scrolls and fires `pointercancel`
- [ ] 3.6 Check against the real thing: `tests/e2e/tests/gestures.spec.ts` on the dashboard's gestures screen, with touch and with a mouse: a hold then a drag moves the card and the list does not scroll; a swipe at once scrolls the list and the card does not move; a tap presses the card's button; and after a mouse's hold and drag the document holds no selection

## 4. Photon

- [ ] 4.1 The paint walk records a `RawGestureDetector` as a pointer target, with its behaviour, as it records a `Listener` (#1562), and the host keeps each detector's recognizers by its `Element`, initialized at every walk and dropped when the position leaves the frame
- [ ] 4.2 `PhotonHost` drives one `GestureRouter`: `PressDown` hands it the detectors `ListenerHits` returns and the built-in members before any branch returns (the canvas's included); `PointerMove`, `PressUp`, `PointerCancel` route; the frame's tick runs `Tick`; `IsFrameDue` includes `NextDeadlineMs`
- [ ] 4.3 The host's candidates become members: the press (wins only at the sweep), the drag and the sort (accept when they arm), the pan and the table pan (accept past the slop), the context hold (accepts when its menu is asked); a member rejected drops its candidate as a pan already drops a press, and a press that lost presses nothing on release
- [ ] 4.4 Check against the real thing: `RawGestureDetectorHostTests` in `tests/eQuantic.UI.Native.Engine.Tests`, a laid-out tree driven through `PhotonHost` with touch and with a mouse, compiling the dashboard's recognizer from its own file (`Compile Include` with a `Link`): a hold then a drag inside a `ScrollView` moves the card and leaves the offset where it was; a swipe at once scrolls and the recognizer hears its rejection; a tap presses the button; a `Draggable` armed first wins; and the whole suite with `--blame-hang`

## 5. The sample and its proof

- [ ] 5.1 `samples/DefaultUIDashboard/Gestures/HoldThenDrag.cs`, the sample's own recognizer: accepts when its pointer held still for `ContextTarget.HoldMs`, rejects when it travels past `Touch.PressCancelSlop` first, then reports each move and the release
- [ ] 5.2 The dashboard's gestures screen (`/gestures`): a `ScrollView` of cards, each wrapped in a `RawGestureDetector` with `HoldThenDrag` that moves the card sideways, each card holding a `Button`; its navigation entry; if `assets/readme/captures.json` names a file this touches, the picture is taken again as CLAUDE.md says
- [ ] 5.3 Check against the real thing: the dashboard built and run, the screen used by hand in Chromium with touch emulation and with a mouse, and the e2e of 3.6 and the host test of 4.4 green

## 6. Docs

- [ ] 6.1 `docs/FLUTTER-PARITY.md` §5: `RawGestureDetector` from GAP to DIFFERENT with the browser's scroll as the trade, `GestureArena` rewritten for the arena that exists; their probes in `FlutterParityPinTests` (`Has("RawGestureDetector") && Has("GestureRecognizer") && Has("GestureArena")`)
- [ ] 6.2 The wiki's Components page, in every language it is written in, on a wiki branch named like this change's branch: the recognizer, the detector, the arena rule per target
- [ ] 6.3 A ledger entry under `docs/ledger/` citing #1563
- [ ] 6.4 Check against the real thing: `FlutterParityPinTests`, the wiki guards with `EQ_WIKI_DIR` on the wiki branch, `./scripts/check-openspec.sh`; then `openspec archive a-gesture-of-the-apps-own-is-recognized --yes` before the merge
