# Spec Delta

## ADDED Requirements

### Requirement: A macOS window gives back what each frame was handed

Each turn of the macOS window's event loop SHALL drain an autorelease pool of its own, so that what a
turn is handed autoreleased, the events it reads and the drawable, command buffer and render pass
descriptor of the frame it presents, is released when the turn ends. A call the Apple shell makes to
Objective-C on a thread of .NET's pool SHALL bring its own pool.

#### Scenario: A window that runs for a number of frames

- **WHEN** the desktop sample runs for 120 frames with `OBJC_DEBUG_MISSING_POOLS=YES`
- **THEN** it presents its frames, and the runtime reports, on the thread the loop runs on, no frame's
  drawable and no render pass descriptor autoreleased with no pool in place

#### Scenario: A loop without its pool

- **WHEN** the same run is made with the loop's turns outside any pool
- **THEN** every frame's drawable and render pass descriptor are reported on that thread, and the check
  in the macOS samples job fails naming how many were left behind over how many frames

#### Scenario: A run that proves nothing

- **WHEN** the run presents fewer frames than it asked for, or the runtime reports nothing at all
- **THEN** the check fails, saying which, rather than passing on a window that never ran or a runtime
  that never read the flag
