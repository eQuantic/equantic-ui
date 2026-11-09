## Context

Slice B's endpoint speaks tus 1.0 at `/api/_equantic/uploads`: a creation names its policy, file and type in `Upload-Metadata`, a part is a PATCH at the offset the server holds, a HEAD reads that offset, a DELETE removes the upload, and a refusal is a status with a short English body. Kestrel holds a request body to 30 MB by default. Slice A's `PickedFile` twin holds the browser's `File` as a `Blob` and its size as a `bigint`.

## How Flutter answers it

Flutter has no upload of its own: `file_picker` hands over the file, and the app sends it with `http`'s `MultipartRequest` or `dio`, or resumes it with the third-party `tus_client_dart`, writing the transport and its progress itself. The nearest first-party shape is Firebase Storage's `UploadTask`: `putFile` answers a task at once, the task has a state (`running`, `paused`, `success`, `canceled`, `error`), the bytes transferred and the total, `pause`, `resume` and `cancel`, and a stream of snapshots. `Upload` is that shape, named in the SDK's own vocabulary, over the SDK's own server.

## Decisions

- **`Upload` is an abstract class, as `PickedFile` is.** Each target realizes it; in the browser the twin IS the realization, so a page never constructs one and the runtime exports the class a page names. An interface would have worked as well for the members, but `File`, `Policy` and `Size` are the same on every target and belong to the base.
- **Changes are heard through `Subscribe(Action<Upload>) → IDisposable`**, the idiom of `INetworkStatus`, `IServerEvents` and `IClock`, not an `OnChanged` callback or a .NET event: a component ends the subscription where it ends, in `OnUnmount`, and a second listener does not replace the first.
- **The rule lives on `UploadPolicy`.** The server's `UploadPolicies.Accepts` and `Formats` moved onto the policy as `Accepts` and `RefusalOf`, which returns the server's words for the first rule the file breaks (the format, then the size); the endpoint answers 415 or 413 with that text. The twin mirrors it with the runtime's own .NET-exact ordinal-ignore-case helpers, and the conformance suite runs both sides.
- **A tus client of our own, over `XMLHttpRequest`.** Only an XHR reports the progress of a body it sends, which is the progress inside a part of a large file. Parts are 8 MB, well under Kestrel's 30 MB. A part that fails (no answer, a 5xx, a 409 offset mismatch, a 423) is sent again after 0, 1, 3, 5 and 10 seconds, each try starting with a HEAD for the offset the server holds; a 4xx is a refusal and its body is the upload's error, never tried again; past the last pause the upload fails with "The connection to the server was lost."
- **Resume after a reload by fingerprint.** The upload's address is kept in `localStorage` under the policy, the file's name, size and `lastModified`, written after the creation and removed on completion, on a cancel and when a HEAD answers 403, 404 or 410; every access is wrapped, so a browser that refuses storage still uploads. The id of the `UploadedFile` is the last segment of the address.
- **`FileUpload` asks its context for both capabilities**, as `CookieConsent` and `CultureSwitcher` ask theirs, because a library component is constructed by the app's factory call and cannot take services in its constructor. It is enabled only once mounted: hydration adopts the server's markup as it finds it, so an area enabled in its first client build stayed disabled on screen (measured on the sample). An area that leaves the screen pauses its unfinished uploads, which choosing the same file again resumes.

## Risks / Trade-offs

- A fingerprint without the content's hash takes two different files of the same name, size, date and policy for one; the server's offset is then wrong for the second. The date makes it unlikely, and hashing a 250 MB file before sending is the larger cost.
- Native shells have no transport yet: `IUploads` is not registered there, and `FileUpload` draws its area disabled.
