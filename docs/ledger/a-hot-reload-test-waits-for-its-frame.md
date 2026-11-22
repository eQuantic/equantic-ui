- **2026-11-22 · A hot reload test waits for the frame the stream writes**: the Windows runner of
  the `v0.2.0-preview.72` tag's run failed `ARebuild_ThatEndsBeforeTheEditLands_WaitsForIt`, the page
  having heard only the `server` frame, and the publish waited on a rerun of the failed jobs. The test
  cancelled its page as soon as `Rebuild` returned, while the `rebuilt` frame is written after it by
  the stream's own loop. Every other test of the class waits for the frame with `UntilAsync` before it
  cancels, and this one now does too
  ([#1818](https://github.com/eQuantic/equantic-ui/issues/1818)).
