## ADDED Requirements

### Requirement: A preview on the web plays its stream in the render that shows it

The browser SHALL attach a camera session's stream to the `<video>` of a `CameraPreview` once the pass that lowered the
preview is written, in that render, with no later pass needed.

#### Scenario: A preview a page's render shows

- **WHEN** a page's render shows a camera preview for a session whose stream started before it, and no later pass
  comes
- **THEN** the preview's video plays the session's stream
