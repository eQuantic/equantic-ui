- **2026-11-12 · A macOS window gives back what each frame was handed**: the macOS window runs its own
  event loop, and nothing drained an autorelease pool around its turns, so every frame left its
  drawable, its command buffer, its render pass descriptor and the GPU's render context behind for as
  long as the window ran. With `OBJC_DEBUG_MISSING_POOLS=YES` the Objective-C runtime counted them: 122
  of each after 122 frames, 482 after 482. Each turn now drains its own pool, and the camera's calls on a
  pool thread bring one, through one `AutoreleasePool` in the Apple shell. `scripts/check-frame-pools.sh`
  runs the desktop sample for 120 frames with the report on, in the macOS samples job, and fails when
  the loop's thread, told apart as the one that reports the window, left a frame's drawable or render
  pass descriptor behind: 244 over 122 frames on the loop without the pool, none with it. It also fails
  on a run that presented fewer frames, that the runtime reported nothing of, or whose window no thread
  reported. Found while measuring
  [#1473](https://github.com/eQuantic/equantic-ui/issues/1473), whose freeze it does not cure: the
  macOS runner stops answering during the first edits after the build with or without the pool, the
  host's caches, Spotlight's indexing or the build's load let fall first. What #1473 gets here is the measurement saying how far an edit
  it did not see drawn got, quoting dotnet watch and the app after the save
  ([#1506](https://github.com/eQuantic/equantic-ui/issues/1506)). Proposed and archived through
  OpenSpec, adding a requirement to `native-app-lifecycle` and one to `hot-reload`.
