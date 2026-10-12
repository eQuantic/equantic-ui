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
where it held all of them. The number is internal: no app compiles it in, and the suite reads it.

**Frames are the clock.** The shells render on demand, so a screen that goes still stops the count,
and what its last frames stopped drawing waits for the next ones. Nothing is uploaded while nothing is
drawn, so the wait holds what was there and adds nothing to it. A clock in seconds would need a timer
that wakes an idle app to release a few textures, which costs more than they do.

**Counted where a frame starts.** `Render` (offscreen) begins one frame and encodes through the inner
step; a window's own pass enters through `Encode`, whose two overloads are one path that begins a
frame. Releasing happens there, before anything is encoded, so nothing bound in the frame being
encoded can be the thing released. The frame that is starting has drawn nothing yet, so it is not
counted among the frames a raster went undrawn for.

**A few a frame.** Leaving a screen retires all of its rasters in the same frame, and each release is a
destroy on the backend. At most sixty-four go as a frame starts and the rest in the frames after, so
the transition that follows is not where the cost lands. A release that throws leaves neither an
entry whose texture is gone nor a list for the next frame to trip over.

**Metal gives the texture back.** The Metal device keeps its fixed objects (queue, pipelines,
samplers) for the life of the process, and `MetalTexture.Dispose` sat behind that same fence as an
empty body. A texture is the one object there whose count grows with what an app draws, so it releases
the reference `newTextureWithDescriptor:` handed over. A command buffer retains what it binds, so a
frame in flight keeps its textures.

**A full pool is a fact about the pool.** With the release in place, the sets live at once follow what
is drawn, so one pool of 1,024 is ample for a screen. The second pool is for the screen that truly
draws more than that at once, and it is what makes the limit stop being a way for the process to end.
Both codes are read, because the driver that was measured answers the older one.

## What is proved where

A counting device in the engine's suite shows what the renderer holds: the last few of a thousand
rasters fed one a frame, one upload for a raster drawn every frame, a second only for one left out of
more frames than it is kept for (a case on each side of the boundary), one slot for a live texture, a
screen of two hundred released sixty-four a frame, and the same count through the overload that takes
layers. Its pass refuses a texture that was already released. With the release switched off, five of
those cases fail.

The real Metal device answers for Metal: four hundred frames, each with a new 256 by 256 raster, and
`currentAllocatedSize` must not grow by the 100 MB that keeping them costs. Once on the waited path,
and once on the unwaited one a window takes, where nothing holds the CPU back and the frames in flight
outnumber the thirty a raster is kept for: what keeps a texture until its frame is drawn there is the
command buffer that bound it. With the empty `Dispose` both fail.

The Vulkan half cannot run on the test hosts, which have no Vulkan device, so it is proved on the
Android emulator (API 36). The text that ended main at its 1,025th set ran past 6,400 different
contents with fewer than a hundred sets ever allocated. A screen of 1,296 different texts drawn at
once took its 1,025th set from a second pool (the first answered `VkResult -12`) and was drawn; left
and opened again, it was drawn from the sets its release gave back, with no third pool.
