- **2026-11-25 · Uploads hold many files at once and several instances of an app**: a site's back office
  lost 15 of 17 photographs a person picked on a phone. Twelve died at their creation: `FileUpload` started
  every file at once, the server allows five unfinished uploads per person, and the browser read the 429
  as a refusal. Three more were finished on one instance and refused by the instance their Server Action
  reached, since the store kept the uploads in one instance's folder and memory. A page now sends three
  files at a time, its first creation alone so a visitor's browser is named once (three sent together each
  named a visitor of its own, and two were refused at their first part), and waits out a 429 that carries
  `Retry-After`, which the server sets on the unfinished limit alone
  ([#1885](https://github.com/eQuantic/equantic-ui/issues/1885)). Where the uploads are kept became the
  app's choice: `IUploadStorage`, with `StoredUpload` as its record and `UseStorage<T>()` on `UseUploads`,
  over which the tus engine, the binding of a Server Action, the limits and the expiry all read; the
  folder stays the default, for one instance, and the server hands a storage only the ids it gives
  ([#1886](https://github.com/eQuantic/equantic-ui/issues/1886)). Proposed and archived through OpenSpec
  (`openspec/specs/file-uploads`).
