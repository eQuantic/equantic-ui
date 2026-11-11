# Tasks

## 1. A render's channels are one object

- [x] 1.1 `RenderChannels`: the style, gradient and face sinks, the expansion scope, the density and the link policy, with `ResolveDestination`. Verify: `LinkSchemeTests` read it
- [x] 1.2 The lowering visitor reads the channels it is handed, and `MarkupHost.Lower` hands them. The six ambients go. Verify: the web suite passes with no pinned markup regenerated, and the parity fixture lowers its page in a language through its channels
- [x] 1.3 The escape hatch's bridge reads the channels `RenderChannels.BridgeWith` armed. Verify: `ABridgeAnEscapeHatchPageComposes_IsBuiltAtTheRequestsDensity`

## 2. The server hands each render its own

- [x] 2.1 `ServerRenderingService` makes one per round and one per discovery, and arms the bridge only around an escape-hatch page. Verify: `TwoRequestsDrawnAtOnce_EachWriteTheirOwnPage`, for a write-once page and an escape-hatch page
- [x] 2.2 The component tester lowers through channels of its own. Verify: the component testing suite passes unchanged
- [x] 2.3 The server suite, the SSR fixtures and the hydration contract. Verify: the server and web suites pass with no pinned markup or payload regenerated
- [x] 2.4 Compiler and Conformance through the lock. Verify: both pass

## 3. The ambients that stay are named

- [x] 3.1 `AmbientChannelsTests` names every `AsyncLocal` in the source with its reason. Verify: it fails on a channel added without an entry, and on an entry whose channel is gone
- [x] 3.2 Docs. Verify: `docs/ELEMENT-TREE-PLAN.md` records E4b, the wiki's Upgrading page carries the break in its two languages, and a ledger entry cites #1404
