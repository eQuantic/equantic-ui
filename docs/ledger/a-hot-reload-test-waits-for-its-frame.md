- **2026-11-22 · A hot reload test waits for the frame the stream writes**: the Windows runner of
  the `v0.2.0-preview.72` tag's run failed `ARebuild_ThatEndsBeforeTheEditLands_WaitsForIt`, the page
  having heard only the `server` frame, and the publish waited on a rerun of the failed jobs. The test
  cancelled its page as soon as `Rebuild` returned, while the `rebuilt` frame is written after it by
  the stream's own loop. It and `AnEventOfAPathAlreadyGone_DoesNotDateTheSaveAfterItsEdit`, which had
  the same race, now wait for the frame with `UntilAsync` before they cancel, as the class's other
  single-save tests do; the two that wait two seconds for a second announcement that must not come stay
  as they are ([#1818](https://github.com/eQuantic/equantic-ui/issues/1818)).
