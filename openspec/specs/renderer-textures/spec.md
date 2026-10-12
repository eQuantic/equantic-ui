# renderer-textures Specification

## Purpose
How long the shared renderer keeps a raster on the GPU after it was last drawn, and what a backend
does when it has no room for one more, so that what an app holds follows what is on its screen.

## Requirements

### Requirement: The renderer keeps uploaded what is drawn

The shared renderer SHALL release the GPU texture of a raster that went undrawn for more than thirty
whole frames, on every backend, and SHALL upload it again when a later frame draws it. A raster drawn
in every frame SHALL be uploaded once. A release SHALL happen as a frame starts, before anything is
encoded, and no more than sixty-four rasters SHALL be released in one frame.

#### Scenario: A thousand rasters, one a frame

- **WHEN** a renderer encodes a thousand frames, each drawing one raster no other frame draws
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

#### Scenario: Every way into the encoder

- **WHEN** a window path encodes five hundred frames through the overload that takes its layers
- **THEN** each one counts as a frame, and the renderer holds the last thirty or so of their rasters

### Requirement: A released texture leaves the device

A backend SHALL give back the device memory of a texture the renderer released, and SHALL keep a
texture a frame still in flight reads until that frame has been drawn.

#### Scenario: Four hundred rasters on Metal

- **WHEN** a Metal backend renders four hundred frames, each with a new 256 by 256 raster
- **THEN** the device's allocated size grows by less than a quarter of what keeping them all costs

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

#### Scenario: More texts on screen than one pool holds

- **WHEN** an app on the Android emulator draws 1,296 different texts in one frame
- **THEN** the 1,025th set comes from a second pool and the screen is drawn, and the same screen left
  and opened again is drawn from the sets its release gave back
