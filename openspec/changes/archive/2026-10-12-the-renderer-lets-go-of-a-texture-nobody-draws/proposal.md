# Proposal

Closes #858, a Bug under #196 (Platform and lifecycle). Found while proving #775 on the Android
emulator, and independent of it: the app in the measurement below has no camera.

## Why

The shared renderer uploaded each raster of a display list once and kept it for its own life.
`RhiRenderer` mapped a `TextureData` to its GPU texture and let one go only when a live texture's
version moved, so a raster nothing drew any more (a text that changed, a row scrolled away) stayed
uploaded until the renderer was disposed.

On Vulkan every uploaded texture also holds descriptor sets, returned only when its image view is
destroyed, out of one pool of 1,024 sets. Measured on the Android emulator (API 36, Vulkan through the
host GPU) on main: a `Text` whose content changes every 16 ms, with the camera closed. The device's
own count read `sets allocated=1000 cached=999 free=0`, the next allocation answered `VkResult -12`,
and the app died with "Vulkan descriptor set allocation failed" about 17 seconds after the text
started changing. A stopwatch, a counter, a long list scrolled or a field typed into for long enough
does the same.

## What Changes

- **The renderer releases what no frame draws.** Each upload remembers the last frame that drew it,
  and a raster that went undrawn for more than thirty whole frames gives its texture back as the next
  frame starts, before anything is encoded. Drawn again later, it is uploaded again from the bytes its
  owner still holds. At most sixty-four go in one frame, so leaving a screen is not one frame that
  pays for all of it.
- **A released texture leaves the Metal device.** `MetalTexture.Dispose` was an empty body behind the
  device's per-process fence, so on Metal the release above would have done nothing. The texture now
  gives back the reference it was created with.
- **A full Vulkan pool hands over to a new one.** `vkAllocateDescriptorSets` answering
  `VK_ERROR_OUT_OF_POOL_MEMORY` or `VK_ERROR_FRAGMENTED_POOL` (what the emulator's driver answered)
  means that pool has no room, and the set then comes from another pool. Every pool goes with the
  device.

For a developer: nothing to write. An app whose text changes for as long as it runs keeps running.

## Capabilities

### New Capabilities

- `renderer-textures`: how long the renderer keeps a raster uploaded, and what happens when a backend
  runs out of room for one.

### Modified Capabilities

None.

## Impact

- **The engine** (`eQuantic.UI.Native.Engine`): `RhiRenderer`.
- **The Metal backend** (`eQuantic.UI.Native.Engine.Metal`): `MetalTexture`.
- **The Vulkan backend** (`eQuantic.UI.Native.Engine.Vulkan`): `VulkanDevice`, and a new
  `DescriptorPoolStatus`.
- **No public surface moves**, and neither does the developer surface. How long a raster is kept is a
  tuning number inside the engine, which its own suite reads so the boundary cases move with it.
  Nothing breaks.
