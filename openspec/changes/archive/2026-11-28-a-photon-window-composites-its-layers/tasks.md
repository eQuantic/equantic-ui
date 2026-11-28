## 1. One way into the renderer (#1952)

- [x] 1.1 `IRhiFrameTarget`, and `RhiRenderer.Render(DisplayList, IRhiFrameTarget)` running every phase into it
- [x] 1.2 Metal: `RenderToDrawable` through `MetalDrawableFrame`
- [x] 1.3 Vulkan: `VulkanSwapchain.Present` through `SwapchainFrame`
- [x] 1.4 The per-command fallback and the `Encode` overloads deleted, and `PopLayer` refusing a pop with no layer open

## 2. The targets a frame draws into

- [x] 2.1 `RhiTargetPool`, for the layers, a backdrop's content, its blur and the blur's levels
- [x] 2.2 Released after thirty frames away, and all of them on a resize

## 3. Proof, documentation and the suites

- [x] 3.1 `AWindowFrameCompositesItsLayersTests` on Metal and Vulkan, failing on main's window paths
- [x] 3.2 `RendererTargetPoolTests`, failing on main and with a pool that keeps the sizes a resize left
- [x] 3.3 The native GPU engine plan's entry on the layers, and a ledger entry under `docs/ledger/`
- [x] 3.4 Every test project of the solution with the Conformance suite in full, each read by its exit code
- [x] 3.5 The macOS jobs on the branch, and the Vulkan tests on a runner's software driver
