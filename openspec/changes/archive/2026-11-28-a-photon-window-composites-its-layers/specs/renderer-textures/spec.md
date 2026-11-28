## MODIFIED Requirements

### Requirement: The renderer keeps uploaded what is drawn

The shared renderer SHALL release the GPU texture of a raster that went undrawn for more than thirty
whole frames, on every backend, and SHALL upload it again when a later frame draws it. A raster drawn
in every frame SHALL be uploaded once. A release SHALL happen as a frame starts, before anything is
encoded. A frame SHALL release no more than sixty-four rasters, or as many as the frame before it
uploaded when that is more, so that what the renderer holds stays bounded however many new rasters
each frame draws.

#### Scenario: A thousand rasters, one a frame

- **WHEN** a renderer draws a thousand frames into a window's frame target, each drawing one raster no
  other frame draws
- **THEN** it holds the last thirty or so, not a thousand

#### Scenario: A raster drawn every frame

- **WHEN** one raster is drawn in each of three hundred frames, beside one that changes every frame
- **THEN** it is uploaded once

#### Scenario: A raster that comes back

- **WHEN** a raster is drawn, left out of exactly thirty frames, then drawn again
- **THEN** it is uploaded once; left out of thirty-one, it is uploaded a second time

#### Scenario: A live texture

- **WHEN** a raster's version moves two hundred times, drawn in each frame
- **THEN** the renderer holds one texture for it throughout

#### Scenario: A screen left

- **WHEN** a frame draws two hundred rasters and the frames after it draw none of them
- **THEN** the first frame to find them away for longer than they are kept releases sixty-four, and
  four frames later none of them is held

#### Scenario: More new rasters a frame than the share

- **WHEN** each of five hundred frames draws a hundred rasters no other frame draws
- **THEN** the renderer holds the last thirty frames' worth or so, not all fifty thousand

#### Scenario: Every way into the encoder

- **WHEN** a window's frame target draws a thousand frames through `Render`, which is now the one way into the
  renderer, each with a raster no other frame draws
- **THEN** each one counts as a frame, and the renderer holds the last thirty or so of their rasters

## ADDED Requirements

### Requirement: The renderer keeps the targets its frames draw into

The shared renderer SHALL take the render targets a frame draws its own passes into (a group's layer, a backdrop's
content, its blur and the blur's levels) from a pool keyed by size and kept across frames, creating one only when the
pool has none of that size free. It SHALL release a pooled target no frame rented for more than thirty whole frames, and
every pooled target when a frame's surface size differs from the frame before it. Disposing the renderer SHALL release
the pool.

#### Scenario: A layer drawn every frame

- **WHEN** a frame with one layer is drawn a hundred times at the same size
- **THEN** the device creates one render target

#### Scenario: The layers of one frame

- **WHEN** a frame holds a layer inside a layer, or three layers side by side, and is drawn fifty times
- **THEN** the device creates two render targets, or three

#### Scenario: A backdrop drawn every frame

- **WHEN** a frame with a backdrop blur is drawn fifty times after its first
- **THEN** no target is created after the first frame's

#### Scenario: A target that is not rented again

- **WHEN** a frame with one layer is followed by thirty frames without one, and one more
- **THEN** its target is still held; followed by thirty-one, it is released

#### Scenario: A window being resized

- **WHEN** thirty frames with a layer are drawn, each at a new size
- **THEN** the renderer holds one target
