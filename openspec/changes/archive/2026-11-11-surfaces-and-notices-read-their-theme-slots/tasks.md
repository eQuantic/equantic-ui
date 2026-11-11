# Tasks

## 1. The slots

- [x] 1.1 `ModalTheme`, `MenuTheme`, `NoticeTheme`, `ProgressTheme` and `CardTheme`, their members on `IAppTheme`, and `With`'s parameters
- [x] 1.2 Each component of the five families reads its slot, its constants as the defaults
- [x] 1.3 `PhotonTheme` fills them with today's sizes, and `MaterialTheme` with Material 3's specifications
- [x] 1.4 The runtime mirrors and builds each from the bridge's JSON, exports them and answers their interface defaults

## 2. The proof

- [x] 2.1 A value in each family's slot reaches the web's lowering and Photon's layout. Verify: `SurfaceSlotsRealizerTests` and `SurfaceSlotsTests`, each case failing with its read put back to the constant
- [x] 2.2 Photon's pinned outputs stay as they were, and the Material goldens are written again after looking at both modes
- [x] 2.3 The runtime's own suites (`tsc` and vitest)
- [x] 2.4 The web, native engine, component testing, server, compiler and conformance suites. Verify: 1408, 3405, 21, 560, 2841 and 7431 tests, and the served runtime's budget re-recorded at 328,128 gzipped bytes (+3,577 from main's 324,551)

## 3. Documentation

- [x] 3.1 One entry for the ledger citing #1243, as a file of its own under `docs/ledger/`
- [x] 3.2 The wiki's DesignSystem page in five languages
