# Tasks

## 1. The vocabulary

- [x] 1.1 `IUploads`, `Upload` and `UploadState` in Primitives, declared in `PublicAPI.Unshipped.txt`, `UploadState` pinned in the enum ABI baseline
- [x] 1.2 `UploadPolicy.Accepts` and `RefusalOf`, which the server's endpoint asks in place of its own copy of the rule, and their twin in the runtime
- [x] 1.3 Server rendering is handed an absent `IUploads` whose uploads have already failed
- [x] 1.4 Prove it: the conformance suite runs the policy's rule on both sides, and fails with the twin's comparison or deduplication taken out

## 2. The browser

- [x] 2.1 The tus transport over `XMLHttpRequest`, and the `Upload` twin that drives it: parts, progress, retry from the server's offset, pause, resume, cancel, resume after a reload
- [x] 2.2 `WebUploads` registered as `IUploads`, and `Upload` exported from both doors
- [x] 2.3 Prove it: vitest against an in-memory tus server, each rule failing with its line taken out

## 3. The component

- [x] 3.1 `FileUpload` and its factory, its strings in every culture resource, transpiled into the runtime
- [x] 3.2 Prove it: vitest drives the transpiled component over stand-in capabilities, and `AdoptConfigCoverageTests` hands it a second configuration

## 4. The sample, against the real server

- [x] 4.1 `/uploads` in the dashboard sample: a policy, `UseUploads`, `FileUpload` and a Server Action that answers the byte count and the SHA-256 of what it read
- [x] 4.2 Prove it: a Playwright spec sends a file of three parts through the real server and checks the hash, and a refusal of the policy's and of the server's shown in the row

## 5. Documentation

- [x] 5.1 The wiki's Uploads page in every language, linked from Capabilities and every sidebar, its C# compiled by `WikiClaimsCompile` and the Server suite
- [x] 5.2 One ledger entry under `docs/ledger/` citing #1215
