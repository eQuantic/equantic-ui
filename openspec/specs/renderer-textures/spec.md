# renderer-textures Specification

## Purpose
How long the shared renderer keeps a raster on the GPU after it was last drawn, and what a backend
does when it has no room for one more, so that what an app holds follows what is on its screen.

## Requirements

### Requirement: The renderer keeps uploaded what is drawn

The shared renderer SHALL release the GPU texture of a raster that no frame has drawn for
`RhiRenderer.RetainFrames` frames, on every backend, and SHALL upload it again when a later frame
draws it. A raster drawn in every frame SHALL be uploaded once.

#### Scenario: A thousand rasters, one a frame

- **WHEN** a renderer encodes a thousand frames, each drawing one raster no other frame draws
- **THEN** it holds no more than `RetainFrames` textures and a few, not a thousand

#### Scenario: A raster drawn every frame

- **WHEN** one raster is drawn in each of three hundred frames, beside one that changes every frame
- **THEN** it is uploaded once

#### Scenario: A raster that comes back

- **WHEN** a raster is drawn, then not drawn for more than `RetainFrames` frames, then drawn again
- **THEN** it is uploaded a second time; away for fewer frames, it is uploaded once

#### Scenario: A live texture

- **WHEN** a raster's version moves two hundred times, drawn in each frame
- **THEN** the renderer holds one texture for it throughout

### Requirement: A full descriptor pool is not the end of the process

The Vulkan backend SHALL take a descriptor set from a new pool when the pool it asked answers that it
has no room, by either code Vulkan answers for it, and SHALL treat any other failure of the allocation
as an error.

#### Scenario: The codes

- **WHEN** `vkAllocateDescriptorSets` answers `VK_ERROR_OUT_OF_POOL_MEMORY` or `VK_ERROR_FRAGMENTED_POOL`
- **THEN** the pool is read as full, and success or any other error is not

#### Scenario: A text that changes for a minute on Android

- **WHEN** an app on the Android emulator draws a text whose content changes every 16 ms for a minute
- **THEN** it keeps running, having drawn more than a thousand different texts
