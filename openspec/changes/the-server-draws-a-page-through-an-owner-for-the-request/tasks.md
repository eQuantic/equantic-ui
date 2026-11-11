# Tasks

## 1. The web's host of an owner

- [x] 1.1 `MarkupHost`: a live or structural owner whose components build through the boundary with a fontless measurer that notes which build asked; `Build`, `Lower`, `EndFrame`. `WebRealizer.LowerFrame` goes. Verify: `FrameLoweringTests` on the host
- [x] 1.2 The lowering marks a component of a frame whose build asked. Verify: `TheFirstFrameOfALiveTree_LowersTheMarkupTheServerWrites` compares whole markup, the mark included, and fails when the mark is dropped
- [x] 1.3 `ServerDataRounds`, the tester's rounds as one implementation. Verify: the component testing suite passes unchanged

## 2. The server draws a write-once page through it

- [x] 2.1 `ServerRenderingService` keeps a structural host for a write-once page and runs its rounds through `ServerDataRounds`; an escape-hatch page draws as before. Verify: `APrefetchRound_DrawsTheInstanceThatLoaded` fails on main and passes here
- [x] 2.2 A navigation's discovery of a write-once page runs the same rounds. Verify: `WhatAComponentLoaded_ThatIsNotAValue_ComposesTheNextRound_OnALoadAndANavigation` fails on main and passes here
- [ ] 2.3 The server suite, the SSR fixtures and the hydration contract. Verify: the server and web suites pass with no pinned markup or payload regenerated
- [ ] 2.4 Compiler and Conformance through the lock. Verify: both pass

## 3. Proved against the real thing

- [x] 3.1 The dashboard sample served, a page with server data loaded in a browser. Verify: `/server-data` (a page that loads, composing a stateless stamp that loads in the next round) carries the process, the runtime and the stamp in the server's markup and payload alike; after hydration, and after a client navigation from another screen in the same document, the page shows the same values, with no console message
- [x] 3.2 Docs. Verify: `docs/ELEMENT-TREE-PLAN.md` records E4a, and a ledger entry cites #1403
