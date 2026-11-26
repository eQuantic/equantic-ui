## Why

Closes #1885 and #1886, both sub-issues of Feature #1212 (an app cannot ask a person for a file and send it to its server).

A real site lost 15 of the 17 photographs a person picked in its back office, for two reasons that each stand on their own:

- `FileUpload` starts every chosen file at once, the server allows each owner five unfinished uploads (`MaxUnfinished`), and the browser reads the 429 the sixth creation gets as a refusal. Twelve photographs failed before a byte was sent, and they would have on a single instance.
- The server keeps the uploads in one instance's folder and their records in its memory. Behind a balancer, each part of an upload and the Server Action that receives it may reach another instance, which answers that the upload does not exist. The site's back office had scaled to three instances; three finished photographs were refused by the instance their action reached. The store's own comment, the wiki's Limits and the archived design all said it served one instance, and an app had no way out: the store is internal, and a shared folder does not help since each instance reads the records once.

## What Changes

- **A page sends a few files at a time.** The uploads one page starts send at most three files at once; the others stay `Waiting` and start, in the order they were started, as a slot frees (a file completes, fails, is paused or is cancelled). Choosing fifty photographs never meets the server's limit of unfinished uploads.
- **A page's first creation goes alone.** Until the server answers it, the other files wait: the cookie that names a visitor's browser is written by the first upload, and creations sent together before it each named a visitor of its own, so all but one were refused at their first part with "This upload belongs to someone else."
- **A limit that frees itself is waited for.** The server answers the owner's unfinished limit with 429 and `Retry-After: 5`; the browser waits that long and tries the creation again, staying `Waiting`, for up to ten minutes before it fails with the server's words. The other limits (the uploads an owner keeps until they expire, the store's total) carry no `Retry-After` and still fail at once.
- **The app chooses where its uploads are kept.** A new public seam, `IUploadStorage`, keeps each upload's record (`StoredUpload`) and bytes: it creates one, finds it, answers how many bytes it holds, appends a part where the content stands, reads the content, deletes it and lists what it keeps. The default keeps them in the folder, as today, which is all one instance needs. An app that runs several registers its own:
  `ui.UseUploads(uploads => uploads.Accept(Photos).UseStorage<S3UploadStorage>())`
  over an object store, a blob container, a database or a shared volume, and every request of an upload, on any instance, is served from it. The limits and the expiry are read from the storage, not from one instance's memory. No type of the seam names the protocol or its library.

## Impact

- `eQuantic.UI.Server`: `IUploadStorage`, `StoredUpload` and `UploadsBuilder.UseStorage<TStorage>()`, declared in `PublicAPI.Unshipped.txt`. The internal store orchestrates over the storage; the folder storage becomes the internal default; an internal adapter is the tus engine's store over any `IUploadStorage`, and `tusdotnet`'s `TusDiskStore` is no longer used. The endpoint stops offering the `creation-with-upload` and `checksum` extensions, which the SDK's client never sends.
- The runtime: `Upload` queues behind a page-wide limit of three, and reads 429 with `Retry-After` as a wait.
- No eqc, web realizer, Photon shell, SDK target or template change. The developer surface gains nothing outside C#.
- Nothing is removed from the public surface, so no app needs a migration line. A folder written by an earlier version holds the engine's side files beside each upload (`<id>.uploadlength`, `.metadata`, `.expiration`, `.chunkstart`, `.chunkcomplete`); the new default reads the records and bytes it finds and ignores those, and the uploads expire in a day as before.

## Principles

- The product principle: an app with several instances writes one C# line and a class over its own storage, the way `UseServerEvents(events => events.UseBackplane<T>())` already reads; no tus vocabulary reaches it.
- The structural fix over the patch: the store stops holding the truth in one instance's memory, rather than a balancer being told to pin a browser to an instance.
