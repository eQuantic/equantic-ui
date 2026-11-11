# Tasks

## 1. The contract reads asynchronously

- [x] 1.1 `ITextClipboard.ReadAsync()` replaces `Read()`; Photon's four shells and the server's absent clipboard answer with `Task.FromResult`. Verify: the solution builds with no warning but the Android shell's CA1422, which main has, and the public API files declare the removal and the addition
- [x] 1.2 The code editor's Paste, the sheet's ⌘V and Photon's text field paste what arrives; the code editor raises `Pasted` and the `CodeEditor` component draws again and tells the app, the sheet refreshes. Verify: `APasteWhoseClipboardAnswersLater_LandsWhenItAnswers_AndSaysSo`, `APasteWhoseClipboardAnswersNothing_LeavesTheDocument` and `APasteWhoseClipboardAnswersLater_LandsWhenItAnswers_AndRefreshesTheViews`, and the native suite
- [x] 1.3 The runtime's transpiled engines follow. Verify: `SharedComponentTranspilationTests` regenerated with `EQ_UPDATE_TRANSPILED=1`, and `tsc` clean

## 2. The web reads later

- [x] 2.1 The code surface's clipboard waits for the browser's paste event on its paste keys and reads through `navigator.clipboard.readText()` on any other key; the page's clipboard service reads the same way. Verify: the two paste cases of `code-editor.spec.ts`, which fail on main's runtime (⌘J pasted nothing, the refused read was never asked) and pass here
- [x] 2.2 The suites. Verify: Web 1410, Server 595, Native.Engine 3387 (30 skipped), Compiler 2857 and Conformance 7801 (1 skipped) pass, `tsc` is clean, and vitest passes 2974 of 2976, the two others being the spreadsheet and table perf specs, which pass on their own under the same load

## 3. Documentation

- [x] 3.1 The wiki's CodeEditor and Capabilities pages in five languages, and the Upgrading page in English and Portuguese, on the wiki branch of the same name. Verify: the wiki guards pass against it
- [x] 3.2 A ledger entry under `docs/ledger/`. Verify: `LedgerTests`
