# frame-passes Specification

## Purpose
How the shared renderer draws a frame on Photon's GPU backends: its group-opacity layers, its backdrops and its last
pass, through one entry, so a window shows what the same frame shows offscreen, where the goldens draw it.

## Requirements

### Requirement: A window frame runs every phase an offscreen frame runs

The shared renderer SHALL draw every frame through one entry, `RhiRenderer.Render(DisplayList, IRhiFrameTarget)`, on a
window and offscreen alike: each group-opacity layer into a target of its own, innermost first, then each backdrop
outside a layer as a split of the frame, then the last pass into the frame target, compositing each layer once. The
Metal backend's drawable path and the Vulkan backend's swapchain present SHALL take that entry. A window SHALL show the
pixels the same frame shows offscreen.

#### Scenario: A group's opacity on a window

- **WHEN** a group at half opacity holds two opaque squares that overlap, over white, and a window draws it on Metal or
  on Vulkan
- **THEN** where the squares overlap is the colour of where only one is, within one level

#### Scenario: A backdrop on a window

- **WHEN** a backdrop blur of radius 6 covers the middle of a page of two-pixel black and white stripes, and a window
  draws it on Metal or on Vulkan
- **THEN** the stripes inside the glass vary by less than a quarter of their contrast outside it

#### Scenario: A window shows what the goldens see

- **WHEN** the same frame is drawn through a window's path and offscreen, on the same device
- **THEN** the two readbacks are byte for byte equal

### Requirement: Every pass before the last is finished before the last begins

The renderer SHALL submit each pass that draws a layer, a backdrop's content or a level of its blur, and SHALL have it
finished, before the frame's last pass begins. The Vulkan device SHALL wait for its queue after each submission, so a
window's pass reads its layers finished.

#### Scenario: The order of a window's passes

- **WHEN** a frame with two layers inside two others is drawn into a window's frame target
- **THEN** all four layer passes are submitted before the window's pass begins, and the window's pass is the last one
  begun and submitted

### Requirement: A layer scope that ends before it begins is refused

The display-list builder SHALL refuse a `PopLayer` with no layer open, and SHALL count the layers a `Replay` opens and
closes, so that `Build` refuses a replay that leaves one open. The encoder SHALL fail on a layer scope that has no target
rather than draw its children at the group's alpha.

#### Scenario: A pop with no layer open

- **WHEN** `PopLayer` is called on a builder with no layer open
- **THEN** it throws `InvalidOperationException`

#### Scenario: A replay that leaves a layer open

- **WHEN** a layer's recorded commands are replayed into a builder without their `EndLayer`
- **THEN** `Build` throws `InvalidOperationException`
