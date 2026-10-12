# Tasks

## 1. The renderer

- [x] 1.1 Each upload remembers the last frame that drew it; a frame's start releases what went undrawn for more than thirty whole frames, at most sixty-four a frame, in `Render` and in both `Encode` overloads. Verify: `RendererTextureReleaseTests` (11 cases) with a counting device whose pass refuses a released texture, and the same tests with the release switched off, where five cases fail

## 2. The backends

- [x] 2.1 `MetalTexture.Dispose` gives back the texture's reference. Verify: `MetalTextureReleaseTests` against the real device's `currentAllocatedSize`, on the waited path and on the unwaited one a window takes; both fail with the empty `Dispose`
- [x] 2.2 `DescriptorPoolStatus.IsFull` reads the two codes, and `VulkanDevice` takes the set from a new pool when the newest is full. Verify: `DescriptorPoolStatusTests`, and on the emulator below

## 3. The device

- [x] 3.1 On the Android emulator (API 36), a text that changes every 16 ms: on main the app dies at its 1,025th set, about 17 seconds in; here it ran past 6,400 different texts with fewer than a hundred sets allocated
- [x] 3.2 On the same emulator, 1,296 different texts in one frame: the 1,025th set came from a second pool after the first answered `VkResult -12`, the screen was drawn, and opened a second time it allocated no further pool

## 4. Documentation and the suites

- [x] 4.1 One `docs/LEDGER.md` line citing #858. No wiki page describes the renderer's texture cache
- [x] 4.2 The Native.Engine suite, alone and read by its exit code: 2,131 passed, 19 skipped, exit 0
