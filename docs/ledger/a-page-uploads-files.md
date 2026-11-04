- **2026-11-04 · A page uploads files: `IUploads`, `Upload` and `FileUpload`**: the third and last slice of
  #1212 ([#1215](https://github.com/eQuantic/equantic-ui/issues/1215)). A page held the files a person chose and
  had no C# way to send them to the endpoint slice B mapped. `IUploads.Start(file, policy)` answers an `Upload`
  with its state, progress, error and result, `Pause`, `Resume`, `Cancel` and an `IDisposable` subscription; the
  browser realizes it over a small tus client of the runtime's own, in parts of 8 MB with progress inside each,
  a failed part sent again from the server's offset after a growing pause, and a page reloaded halfway going on
  with the same file from an address kept in `localStorage`. `UploadPolicy.Accepts` and `RefusalOf` are now the
  one rule the page asks before it sends and the server asks again, in the same words. `FileUpload` is the drop
  area, the button and a row per file, its strings in every culture the SDK ships, enabled only once mounted
  because hydration keeps the server's disabled markup (measured on the sample). Proved by vitest against an
  in-memory tus server and over the transpiled component, the conformance suite for the policy's rule, and a
  Playwright spec that sends a file of three parts through the sample's real server and checks the SHA-256 its
  Server Action read. The served runtime grows by about 4.8 KB gzipped. Proposed through OpenSpec
  (`file-uploads`).
