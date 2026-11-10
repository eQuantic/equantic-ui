## ADDED Requirements

### Requirement: A save is announced once

The server SHALL rebuild and announce only for the saves made since the last rebuild took its saves, so a
second call for a save the last rebuild answered SHALL rebuild nothing and send the page nothing. Every
rebuild that ends SHALL say on the server's console whether the page hydrates or mounts, and for a mount,
when the save was written and when the last edit reached the server.

#### Scenario: A second call for an answered save

- **WHEN** a save is rebuilt and announced, and the debounce asks for a rebuild again with nothing saved since
- **THEN** the page hears one announcement, where it heard a second one, which said mount

#### Scenario: A page that mounts

- **WHEN** a rebuild tells the page to mount
- **THEN** the server's console says when the save was written and when the last edit reached it, or that no save brings it its edit
