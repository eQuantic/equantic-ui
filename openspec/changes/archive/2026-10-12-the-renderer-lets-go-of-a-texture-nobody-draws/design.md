# Design

## How Flutter solves it

Flutter's engine keeps rasters in caches with a budget and an eviction rule rather than for the life
of the renderer: Skia's resource cache is sized in bytes and purges what has not been used, and the
raster cache drops entries that were not touched in the frame. What a frame stops drawing leaves. This
change gives the renderer the same property with the smallest rule that has it: an age in frames. No
row of `docs/FLUTTER-PARITY.md` covers the GPU resource caches, so none changes.

## Decisions

**Age in frames, not a byte budget.** The defect is unbounded growth, and what bounds it is "not drawn
any more". A byte budget would need each backend to account for its own memory and would still have
to decide what goes first, which is the same age. A frame count needs one number per upload.

**Thirty frames.** Long enough that what leaves the screen for a moment (a row scrolled past and
back, a state that flickers, a tab switched and switched back within half a second) is not uploaded
again, and far longer than any frame still in flight on the GPU, so a released texture is never one a
pending command buffer reads. Short enough that a text changing every frame holds thirty textures,
where it held all of them.

**Counted where a frame starts.** `Render` (offscreen) and `Encode(displayList, commands)` (a window)
each begin one frame; the three-argument `Encode` is the inner step both use. Releasing happens there,
before anything is encoded, so nothing bound in the frame being encoded can be the thing released.

**A full pool is a fact about the pool.** With the release in place, the sets live at once follow what
is drawn, so one pool of 1,024 is ample for a screen. The second pool is for the screen that truly
draws more than that at once, and it is what makes the limit stop being a way for the process to end.
Both codes are read, because the driver that was measured answers the older one.

## What is proved where

A counting device in the engine's suite shows what the renderer holds: the last few of a thousand
rasters fed one a frame, one upload for a raster drawn every frame, a second only for one that left
longer than it is kept, one slot for a live texture. With the release switched off the same tests fail.
The Vulkan half cannot run on the test hosts, which have no Vulkan device, so it is proved on the
Android emulator: the text that ended main at its 1,025th set ran past 3,600 different texts with fewer
than a hundred sets ever allocated.
