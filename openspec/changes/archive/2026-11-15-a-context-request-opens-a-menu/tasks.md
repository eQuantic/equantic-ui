## 1. The blocks

- [x] 1.1 `ContextTarget`, `ContextRequest` and `Anchored.At` in Primitives, with every registration a node has
- [x] 1.2 `UI.ContextTarget` and `ContextMenu`, the `Menu`'s panel shared

## 2. Web

- [x] 2.1 The lowering's mark and event on both producers; `Anchored.At` on both
- [x] 2.2 `dom/context-request.ts`: `contextmenu` and a finger's hold, the nearest target, the browser's menu kept out

## 3. Photon

- [x] 3.1 Context regions in the paint walk; `PhotonHost.ContextRequest`, the hold, `ContextMenu` and Shift+F10; `Anchored.At`
- [x] 3.2 The shells: macOS right button and Control-click, Windows right button and Applications key, Android secondary button and menu key, iOS secondary click

## 4. Proof, sample, docs

- [x] 4.1 Tests on both targets: the pointer, the hold, the keys, the nearest target, a hold that lifts a sortable item, the browser's menu; `ContextMenu` opened at the point and by the keys
- [x] 4.2 The dashboard's `/files` screen opens a menu on the open file (the tree's menu per node is #1532's)
- [x] 4.3 The wiki's Components page in every language, the parity row, a ledger entry; archive
