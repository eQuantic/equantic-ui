- **2026-11-04 · The server receives files in resumable parts and hands them to a Server Action**: the second
  slice of #1212 ([#1214](https://github.com/eQuantic/equantic-ui/issues/1214)). A Server Action took JSON of at
  most 1 MB, so a file of real size had no way to reach the app's server. An app now declares an `UploadPolicy`
  once, in C# its pages and its server both read (a name, the formats as `FileFilter`s, the largest size), and
  `UseUploads(u => u.Accept(policy))` on `AddUI` maps `/api/_equantic/uploads`, which speaks tus through
  `tusdotnet` with no tus type public. A creation the policy does not accept is refused with a reason (400, 401,
  413, 415, 429); an upload belongs to the signed-in person or else to the browser, by a strictly necessary
  cookie, and only its owner continues, inspects, deletes or receives it, after a restart too; `EQuantic:Uploads`
  sets the folder, the unfinished uploads allowed and the expiry, past which a hosted service removes an upload
  finished or not, since `tusdotnet`'s own expiry removes only the unfinished (measured). A Server Action takes
  an `UploadedFile`, bound from its id alone for its owner once complete, and streams it with `OpenRead`, which
  the build refuses in client code. Proved by real tus requests through the real endpoint, each owner, limit and
  sweep rule failing with its check taken out, and by the conformance suite for the three runtime twins.
  Proposed through OpenSpec (`file-uploads`).
