## Why

Closes #1952, a Bug under #1587, found while designing #1938, which it blocks on Photon.

On a live Photon window a group's opacity blended each of its draws on its own, so where two children
overlapped the group showed through itself, and a `BackdropBlur` drew no blur. `MetalBackend.RenderToDrawable` and
`VulkanSwapchain.Present` opened their own pass and called `RhiRenderer.Encode` into it, which skipped the phase that
draws each layer into a target of its own: the encoder fell back to multiplying each command's alpha, and skipped every
backdrop. Only `RhiRenderer.Render`, the offscreen path the goldens and the parity tests take, ran that phase, which is
why no golden saw it. Measured through each window path on this Mac, a red group at half opacity over white reads
(230, 188, 188) where one child is and (216, 137, 137) where two overlap, and a glass over sharp stripes keeps their full
contrast.

The layer targets were also created at the surface's size and disposed in every frame. At 2880 by 1800 one layer is
20.7 MB of allocation a frame.

#1938's fade draws a subtree into a layer and multiplies it by a gradient's alpha. A mask has no per-command
approximation, so on a window it cannot draw until both window paths composite real layers.

## What Changes

Nothing an app writes changes. A group's opacity composites once and a backdrop blurs on a Photon window, on Metal and on
Vulkan, as they already did offscreen.

- **One way into the renderer.** `RhiRenderer.Render(DisplayList, IRhiFrameTarget)` draws every frame through the same
  phases: each layer into a target of its own, innermost first, then each backdrop's split, then the last pass, into
  the frame target. `IRhiFrameTarget` is that last pass: where it draws and what submitting it means.
  - **Offscreen**, it is a render target the caller reads back, and its submit waits.
  - **On Metal**, `RenderToDrawable` draws the last pass into the drawable and presents it unwaited
    (`MetalDrawableFrame`).
  - **On Vulkan**, `VulkanSwapchain.Present` draws it into the acquired image, waiting on the acquire semaphore and
    signalling the present's (`SwapchainFrame`).
- **The fallback is deleted.** The encoder composites a layer from its target, and a scope without one fails rather
  than drawing each child at the group's alpha. `DisplayListBuilder.PopLayer` refuses a pop with no layer open, and
  `Replay`, which a departing subtree and a sorted item draw through, counts the layers it replays, so `Build` refuses
  a list with such a scope before a frame meets it.
- **Every pass before the last is finished before the last begins.** Each is submitted and waited for: the Vulkan
  device's submit waits for its queue to go idle, so the window's pass reads a finished layer, and the uniform ring every
  pass writes from its start is free again. The order is measured on the device, not assumed.
- **The targets come from a pool** (`RhiTargetPool`), keyed by size and kept across frames: a group's layer, a
  backdrop's content, its blur and the blur's levels. A target no frame rents for thirty frames is released, as a raster
  is, and every target is released when the surface changes size, so a window being resized holds one frame's targets.

Parts reached: the native engine's renderer and builder, and the Metal and Vulkan backends. The experimental native
surface moves: `IRhiFrameTarget` and `RhiRenderer.Render(DisplayList, IRhiFrameTarget)` join it, and the two
`RhiRenderer.Encode` overloads and the public `RhiRenderer.Blur` leave it. `DrawUniforms.TryBuildTiled` loses its
`layerAlpha` parameter, which only the fallback set. No app calls any of them: the shells reach the renderer through
the backends. The one migration line, for code that drove the renderer itself: call `Render` with a frame target in
place of `Encode` with a pass of its own.

This follows docs/PRINCIPLES.md's preference for the structural fix: the window and the goldens take one path, so a
golden that passes says what a window shows. It removes the members the change replaced rather than keeping them
beside it.

## Capabilities

### New Capabilities

- `frame-passes`: how the shared renderer draws a frame, its layers, its backdrops and its last pass, the same on a
  window as offscreen.

### Modified Capabilities

- `renderer-textures`: the targets a frame draws its own passes into are pooled across frames, and every frame takes
  the one way into the renderer.

## Impact

- `AWindowFrameCompositesItsLayersTests` draws a group at half opacity and a glass over stripes through Metal's
  `RenderToDrawable` and Vulkan's `SwapchainFrame`, finds the group composited once and the glass blurred, and the
  window's pixels equal to the same frame drawn offscreen. All four fail on main's window paths.
- `RendererTargetPoolTests` holds the pool: one target for a layer drawn every frame, one per layer of a frame, a
  backdrop's targets made once, every layer submitted before the window's pass begins, a target released after thirty
  frames away, and a resize that holds one frame's targets. On main a layer drawn a hundred times makes a hundred
  targets, and a pool that kept the sizes a resize left holds thirty.
