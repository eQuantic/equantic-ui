## 1. The engine

- [x] 1.1 `BuildOwner.DrewOnly` and `BuildOwner.WaitsForALayer`, and `EndFrame` sweeps only inside the parts a frame drew
- [x] 1.2 The engine's TypeScript twin generated again (`EQ_UPDATE_TRANSPILED=1`)
- [x] 1.3 `ElementTreeTests`: a layer outside what a frame drew stays, one inside it that was not shown leaves, and a marked position waits for its layer

## 2. The browser

- [x] 2.1 `relowerBuilt` says what it drew, nothing when it falls back, and falls back when a position waits for a layer
- [x] 2.2 `subtree-relowering.spec.ts`: an open overlay and an open anchored panel outside a patch stay mounted and live, and a panel closed in a patch leaves
- [x] 2.3 Proved both ways: without the fix the overlay case fails with `['b']` unmounted

## 3. Photon and the documentation

- [x] 3.1 `ElementHostTests`: a `SetState` outside two open layers leaves them mounted, and one inside the overlay builds in the next frame
- [x] 3.2 One ledger entry under `docs/ledger/`
