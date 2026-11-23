## 1. The menu

- [x] 1.1 `MenuPanel`'s items set `CanRequestFocus = false`, and the twin is generated again
- [x] 1.2 Photon (`ModalFocusReturnTests`): a row's menu, its Delete, the confirm, Cancel, by a pointer and by the keyboard, and the focus is on the menu's trigger; the pointer case fails without the change

## 2. The browser

- [x] 2.1 The traps are reconciled in the microtask after the write (`scheduleFocusTrapCommit`)
- [x] 2.2 `menu-dialog-focus.spec.ts` runs the same case; without the deferral both fail, without the menu change the pointer case fails
- [x] 2.3 `focus-trap.spec.ts` gains a case through a page's own render and write, which fails with the commit before the write

## 3. The documentation

- [x] 3.1 One ledger entry under `docs/ledger/`
