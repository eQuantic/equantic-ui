## 1. The browser

- [x] 1.1 `focus-trap.ts` asks the gates around a layer when the Tab or the focus arrives (`onScreen`), and the topmost layer on screen owns the keyboard
- [x] 1.2 `focus-trap.spec.ts`: a drawer in the compact arm at 700, 1024 and 700 again; a layer outside every arm at every width; a dialog under a hidden drawer owns the keyboard
- [x] 1.3 Proved both ways: without the change the drawer pulls back a focus on the sidebar at 1024, and the dialog under it does not cycle Tab

## 2. The documentation

- [x] 2.1 One ledger entry under `docs/ledger/`, and the wiki's `Runtime` page in every language
