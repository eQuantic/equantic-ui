# Tasks

## 1. The server

- [x] 1.1 Count the edits applied to the server through `MetadataUpdateHandler`, and announce a rebuild once the edit of its save arrived, saying `hydrate` or `mount`. Verify: the stream tests see `hydrate` for an edit that lands before or after the rebuild ends, `mount` at once outside dotnet watch and after the limit when none comes, and the wait tests fail with the wait removed
- [x] 1.2 Name the run of the server on every connection, and send a failed rebuild's errors. Verify: two connections to one run read the same name, a second run another, and a failed rebuild sends MSBuild's error lines and no reload
- [x] 1.3 Set dotnet watch's guard in the shell while the stream runs. Verify: the decision test finds it exactly on the pages that listen, and the guard test reads it out of the SDK global.json selects and fails with another name

## 2. The page

- [x] 2.1 Listen to `server`, `rebuilt` and `failed`, ask for one reload, and tell the next boot whether to hydrate. Verify: the boot's hot reload spec
- [x] 2.2 Hydrate markup that holds the edit and replay the fields after it. Verify: the served element is kept and draws the replayed field, with no hydration warning, and the old boot fails the case
- [x] 2.3 Show a failed rebuild's errors in the development overlay, as text

## 3. The proof

- [x] 3.1 Make the measurement fail when a save reloads the page more than once, or the reloaded document does not hydrate, or warns. Verify: a local run at a load average of 70 reports one document per save, hydrated with no warning, the replay at 37 to 42ms
- [x] 3.2 Prove it in a browser: the page stays on its document while the edit reaches the server, reloads once when the rebuild ends, hydrates the new text with the counter it held, and the console has no hydration warning. A restart reloads it once more, by the run's new name
- [ ] 3.3 Lower the web's ceiling in `scripts/edit-to-view.budget.json` by what was measured, once #1261 has set it from CI

## 4. Documentation

- [x] 4.1 One entry for the ledger citing #1275, as a file of its own under `docs/ledger/`
- [x] 4.2 The wiki's hot reload paragraph in five languages
