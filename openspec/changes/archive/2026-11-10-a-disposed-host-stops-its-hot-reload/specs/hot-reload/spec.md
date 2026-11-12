## ADDED Requirements

### Requirement: Hot reload ends with its host, stopped or not

The server SHALL end hot reload, its watchers, its timers and the compiler it started, when the app stops
and when the host's services are disposed, whichever comes first, and ending it a second time SHALL do
nothing more.

#### Scenario: A host disposed without being stopped

- **WHEN** an app with hot reload on is started and its host disposed without being stopped, and a source under its content root is then written
- **THEN** no rebuild starts, where the watcher still running started one
