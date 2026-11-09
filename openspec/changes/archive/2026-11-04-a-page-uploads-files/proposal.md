## Why

Closes #1215, the third and last slice of Feature #1212 (an app cannot ask a person for a file and send it to its server).

Slice A (#1213) lets a person choose files and drop them, and slice B (#1214) gives the server an endpoint that receives them in resumable parts and a Server Action that reads them. Between the two there is nothing: a page holds `PickedFile`s and has no C# way to send them, so it would have to write a tus client in JavaScript, which the product principle rules out.

## What Changes

- **A page sends a file with one call.** `IUploads` (Primitives), a capability taken through the constructor or asked of the context like `IFilePicker`, starts an `Upload` from a `PickedFile` and an `UploadPolicy`: `var upload = uploads.Start(file, Uploads.Models);`. The `Upload` reports `State` (a new `UploadState`: `Waiting`, `Sending`, `Paused`, `Completed`, `Failed`, `Cancelled`), `Sent` and `Size`, `Error` and, once complete, `Result`, the `UploadedFile` a Server Action takes; `Pause`, `Resume` and `Cancel` act on it, and `Subscribe` hears its changes as an `IDisposable`, as `INetworkStatus` does.
- **The policy states its own rule.** `UploadPolicy.Accepts(fileName)` and `UploadPolicy.RefusalOf(fileName, size)` are the one rule the page asks before it sends a byte and the server asks again, in the same English words; the server's internal copy of the rule is gone.
- **`FileUpload`**, a write-once component (`FileUpload(policy, onCompleted, multiple, label, hint)`): a drop area, a button that asks the picker for the policy's formats, and a row per file with its progress, state, Pause, Resume, Cancel and error, announcing what arrives or fails. Its strings are the SDK's in every culture the SDK ships.
- **The browser sends in parts it can resume**, over a small tus 1.0 client of the runtime's own (no dependency): parts of 8 MB with progress inside each, a failed part sent again from the server's offset after a growing pause, a page reloaded halfway going on with the same file through an address remembered in `localStorage`, and a cancel that removes the upload on the server.
- **Server rendering** is handed an `IUploads` that is not available and whose uploads have already failed, so a page that takes one renders there; `FileUpload` draws its area disabled until it is mounted in the browser.

## Impact

- `eQuantic.UI.Primitives`: `IUploads`, `Upload`, `UploadState`, `UploadPolicy.Accepts` and `UploadPolicy.RefusalOf`, declared in `PublicAPI.Unshipped.txt`. `UploadState` is appended to the enum ABI baseline.
- `eQuantic.UI.Components`: `FileUpload` and its `UI.FileUpload` factory, seventeen SDK strings in the neutral, `pt-BR` and `es` resources.
- The runtime: the `Upload` twin, which is the browser's realization, the tus transport, `WebUploads` registered as `IUploads`, the `UploadPolicy` twin's rule, the transpiled `FileUpload`. The served runtime grows by about 4.8 KB gzipped, re-recorded in its budget.
- `eQuantic.UI.Server`: the absent `IUploads` for server rendering; the endpoint asks the policy's rule.
- No eqc, template, Photon shell, SDK target or developer-surface change. Nothing is removed from a released surface, so no app needs a migration line.
