## 1. The block

- [x] 1.1 `Listener`, `PointerEvent`, `PointerButtons`, `HitTestBehavior` and `PointerPhase.Scroll` in Primitives, with every registration a node has
- [x] 1.2 `UI.Listener`

## 2. The hit rule

- [x] 2.1 `listener-hits.fixture.json`: the cases of the rule, in entries any target can build
- [x] 2.2 The rule in C# (`ListenerHits`) and in TypeScript (`listenerHits`), each running every case of the fixture

## 3. Web

- [x] 3.1 The lowering's mark and event on both producers
- [x] 3.2 `dom/pointer-listener.ts`: the browser's hit test, the solids, the pointer followed from its down, hover, scroll
- [x] 3.3 Proved in a real browser: the dashboard's listener heard in the gap only when it is not deferring, and a press followed outside the box

## 4. Photon

- [x] 4.1 The paint walk's listeners and solids, recorded only after the first listener
- [x] 4.2 The host: down, move, up, cancel, hover, scroll, and the secondary click from `ContextRequest`; the macOS shell's Control-click as a primary press
- [x] 4.3 Proved through the host with a laid-out tree: the gap, the stack, the followed pointer, the button inside

## 5. Docs

- [x] 5.1 The wiki's Components page in every language, the two parity rows, a ledger entry; archive
