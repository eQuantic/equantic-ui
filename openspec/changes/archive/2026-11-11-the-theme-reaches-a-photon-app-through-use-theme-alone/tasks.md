# Tasks

## 1. The theme leaves the bound options

- [x] 1.1 `PhotonOptions.Theme` goes, `UseTheme` keeps the theme, and `PhotonApplication.Theme` carries it to the four shells. Verify: `TheThemeReachesTheAppThroughUseThemeAlone`
- [x] 1.2 The bound options reach no theme, and the binder's suppression goes. Verify: `TheBoundOptions_ReachNoTheme`, which fails when its walk starts at a type that holds a theme, and `eQuantic.UI.Native.Hosting` builds with no SYSLIB1100 or SYSLIB1101
- [x] 1.3 The samples and the `equantic-native` template call `UseTheme`

## 2. The proof

- [x] 2.1 The native engine (3386) and web (1408) suites, and the README's pictures of the two samples taken again on the desktop, an iOS simulator and an Android emulator. Verify: the desktop pictures are pixel-identical, the phones' differ by what main changed since they were taken (text weights, the emulator's status bar) and by a line #1442 follows

## 3. Documentation

- [x] 3.1 One entry for the ledger citing #1398, as a file of its own under `docs/ledger/`
- [x] 3.2 The wiki's Upgrading page, in its two languages
