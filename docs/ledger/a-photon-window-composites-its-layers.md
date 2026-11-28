- **2026-11-28 · A Photon window composites a group's opacity once and blurs a backdrop, as the goldens
  always did**: the windows' paths, `MetalBackend.RenderToDrawable` and `VulkanSwapchain.Present`, opened
  a pass of their own and encoded into it, skipping the phase that draws each layer into a target of its
  own, so a group at half opacity read (216, 137, 137) where two of its children overlapped and (230, 188,
  188) where one was, and a glass over sharp stripes kept their full contrast. The offscreen path the
  goldens take was right, which is why none saw it. Every frame now takes one entry,
  `RhiRenderer.Render(DisplayList, IRhiFrameTarget)`, whose frame target is the last pass: a render target
  offscreen, the drawable on Metal, the acquired image on Vulkan. The per-command fallback and the two
  `Encode` overloads are gone, `PopLayer` refuses a pop with no layer open, and the targets a frame draws
  into come from a pool kept across frames, which a resize empties. They used to be created and disposed
  in every frame, 20.7 MB a frame for one layer at 2880 by 1800. This unblocks #1938's fade on Photon
  ([#1952](https://github.com/eQuantic/equantic-ui/issues/1952)). Proposed and archived through OpenSpec
  (`openspec/specs/frame-passes`, `openspec/specs/renderer-textures`).
