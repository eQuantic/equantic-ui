## Why

Closes #1214, the second slice of Feature #1212 (an app cannot ask a person for a file and send it to its server).

A Server Action takes JSON of at most 1 MB, so a file of any real size has no way to reach the app's server, and a web app that needs a person's model, photo or CSV drops to the `HtmlElement` escape hatch and still has no C# way to carry the content. The server needs to receive files in parts that survive a dropped connection, keep each one for its owner alone, and hand it to a Server Action. Slice A (#1213) lets a person choose the file; slice C (#1215) is the browser's client and the `FileUpload` component that send it here.

## What Changes

- **A policy, declared once in C# both sides read.** `UploadPolicy` (Primitives) names a kind of upload, its formats as `FileFilter`s, its largest size and whether it requires a signed-in person:
  `public static readonly UploadPolicy Models = new("models", filters: [new FileFilter("3D models", "stl", "obj")], maxSize: 250 * 1024 * 1024);`
- **The server takes uploads under the policies the app accepts.** `builder.Services.AddUI(ui => ui.UseUploads(u => u.Accept(Models)))` maps `/api/_equantic/uploads` beside the Server Actions when `MapUI` runs. The endpoint speaks the tus 1.0 protocol through `tusdotnet`, an implementation detail no public type names. A creation that names an unknown policy, a format its policy does not take, a size past its limit, a visitor under a policy that requires signing in, or an owner at the unfinished uploads allowed is refused with a status and a short English reason a page can show.
- **An upload is its owner's.** The owner is the signed-in person, or else the browser, by a strictly necessary cookie (`eq-uploads`). Only the owner continues, inspects, deletes or receives an upload, after a restart too.
- **`EQuantic:Uploads` in appsettings** sets the folder (`Folder`), the unfinished uploads one owner may have (`MaxUnfinished`, 5) and how long an upload is kept (`Expiry`, a day). The folder is a staging area: what is still there past its expiry is removed, finished or not.
- **A Server Action takes an `UploadedFile`.** `UploadedFile` (Primitives) is an upload as the page knows it; a Server Action parameter of that type is bound from the upload's id alone, for the request's owner and once the upload is complete, and its `[ServerOnly]` `OpenRead` streams the content from the disk without holding it in memory; `Delete` removes it.
- **The Server Actions' origin check is shared.** The upload endpoint refuses another site's request by the same rule and the same `EQuantic:ServerActions:AllowedOrigins` list.

## Impact

- `eQuantic.UI.Primitives`: `UploadPolicy`, `UploadedFile`, declared in `PublicAPI.Unshipped.txt`.
- `eQuantic.UI.Server`: `UIOptions.UseUploads`, `UploadsBuilder`, `UploadsOptions`, declared in `PublicAPI.Unshipped.txt`; the internal endpoint, store, owner, cleaner and binding; a package dependency on `tusdotnet` 2.12.0; `ServerActionsMiddleware` binds an `UploadedFile` parameter.
- The runtime: twins of `UploadPolicy`, `UploadedFile` and `FileFilter`, which a policy built in a page's module constructs (`FileFilter` leaves the list of types that owe no twin). The served runtime grows by about 0.4 KB gzipped, inside its budget.
- eqc: no change; the `[ServerOnly]` members of a type that crosses are fenced by the rule that exists (EQ2010).
- The developer surface gains `config EQuantic:Uploads`. Nothing is removed, so no app needs a migration line.
- No template, no Photon shell and no SDK target changes.
