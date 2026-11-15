# Proposal

Closes #1506, a Bug under #1174. Part of #1473, which stays open.

## Why

The macOS window runs its own event loop, and nothing drained an autorelease pool around its turns. A
loop a shell runs itself has no pool unless it brings one, so what each turn was handed autoreleased
lived as long as the process: the events it read and, every frame, the drawable, the command buffer,
the render pass descriptor and the GPU's render context. With `OBJC_DEBUG_MISSING_POOLS=YES` the
Objective-C runtime reports each one, and the desktop sample left 122 drawables after 122 frames and
482 after 482. Nothing failed: the window drew and every test passed.

The camera starts and stops its session on a thread of .NET's pool, which has no pool either.

And #1473: the desktop edit-to-view measurement waited out its 300 seconds for a window that never drew
an edit, and said only that. It could not tell an edit that never reached the app from one that
reached it and was never drawn. Eleven rounds on the runner since found that the macOS runner itself
stops answering during the first edits after the build, with or without the pool, the host's caches
or Spotlight's indexing. That cause is not fixed here; what the measurement says when it happens is.

## What Changes

- Each turn of the macOS window's loop drains its own pool, and the camera's calls on a pool thread
  bring one.
- `scripts/check-frame-pools.sh` runs the desktop sample for 120 frames with the runtime's report on
  and fails when a frame left its drawable behind, when the run presented fewer frames, or when the
  runtime reported nothing at all. The macOS samples job runs it.
- The hot reload says, as an edit arrives, how many types it changed and how many windows it woke,
  and the desktop measurement, when a window does not draw an edit, says what dotnet watch and the app
  said after the save and whether dotnet watch is still running.

## Impact

- `src/eQuantic.UI.Native.Shell.MacOS/PhotonWindow.cs`, `src/eQuantic.UI.Native.Shell.Apple/ObjC.cs`
  and `AppleCamera.cs`; `AutoreleasePool`, over `objc_autoreleasePoolPush` and
  `objc_autoreleasePoolPop`, joins the Apple shell, which is experimental surface.
- `src/eQuantic.UI.Native.Components/PhotonHotReload.cs`, `scripts/measure-edit-to-view.cs`,
  `scripts/check-frame-pools.sh` and the samples job.
- An app's macOS window holds the same memory after a thousand frames as after ten. No API an app
  writes changes.
