# Tasks

## 1. The contract

- [ ] 1.1 `IFileSaver` and `FileSaveOutcome` in the vocabulary, with the media type derived from the name. Verify: `PublicAPI.Unshipped.txt` declares them, and the enum's runtime twin exists
- [ ] 1.2 The server render's absent saver. Verify: a page taking `IFileSaver` renders on the server, and a call there throws with the reason (`tests/eQuantic.UI.Server.Tests`)

## 2. The web

- [ ] 2.1 `devices/file-saver.ts`, registered as `IFileSaver`. Verify: vitest specs for the text and bytes paths, the derived media types, the anchor removed and the URL revoked, and nothing awaited before the click
- [ ] 2.2 A transpiled page calls it. Verify: eqc resolves `IFileSaver` to the runtime's service and the call reaches `saveTextAsync`
- [ ] 2.3 The dashboard sample's button saves a text file. Verify: in a real browser, pressing it downloads the file with its name and content

## 3. The desktop

- [ ] 3.1 One realization over `IFileDialogs`, registered by the macOS and Windows shells. Verify: a test with a fake dialog writes the file and reads it back, and a cancel writes nothing

## 4. Documentation

- [ ] 4.1 The wiki's Capabilities page in its five languages: the table row, a section, and the shells matrix, with the phones left to #782
- [ ] 4.2 One entry for the ledger citing #1022, as a file of its own under `docs/ledger/`
- [ ] 4.3 A comment on #782 saying the contract it realizes on the phones is `IFileSaver`
