# Tasks

## 1. The renderer

- [x] 1.1 Each upload remembers the last frame that drew it; `BeginFrame` releases what was not drawn for `RetainFrames` frames, in `Render` and in the window path's `Encode`. Verify: `RendererTextureReleaseTests` (7 cases) with a counting device, and the same tests with the release switched off, where the thousand-raster case and the comes-back case fail

## 2. The Vulkan backend

- [x] 2.1 `DescriptorPoolStatus.IsFull` reads the two codes, and `VulkanDevice` takes the set from a new pool when the newest is full. Verify: `DescriptorPoolStatusTests`, and on the emulator below

## 3. The device

- [x] 3.1 On the Android emulator (API 36), a text that changes every 16 ms: on main the app dies at its 1,025th set, about 17 seconds in; here it ran a minute, past 3,600 different texts, with fewer than a hundred sets allocated

## 4. Documentation and the suites

- [x] 4.1 One `docs/LEDGER.md` line citing #858. No wiki page describes the renderer's texture cache
- [x] 4.2 The Native.Engine suite, alone and read by its exit code: 2,125 passed, 19 skipped, exit 0
