## ADDED Requirements

### Requirement: A save reloads the page once, into markup that holds the edit

The rebuild stream SHALL be the page's one source of reloads: while it runs, the page SHALL keep
dotnet watch's refresh script from connecting. A rebuild SHALL announce itself once the edit of the
save it answers has reached the server, when dotnet watch runs the server, and SHALL say whether the
server's markup holds the edit. The page SHALL hydrate markup that holds the edit and replay its
fields after hydrating it, and SHALL draw over markup that does not.

#### Scenario: One reload per save

- **WHEN** `scripts/measure-edit-to-view.cs web` times an edit of the dashboard sample
- **THEN** each save reloads the page once, the reloaded document hydrates the server's markup, and it
  raises no hydration warning, or the run fails

#### Scenario: The edit reaches the server after the rebuild

- **WHEN** a rebuild ends before the edit of its save has reached the server, and dotnet watch runs it
- **THEN** the rebuild announces itself once the edit arrives, and says the page hydrates

#### Scenario: No edit reaches the server

- **WHEN** a rebuild ends and no edit reaches the server within two seconds of the save, or the server
  runs outside dotnet watch
- **THEN** the rebuild says the page mounts, and the page draws over the server's markup

### Requirement: The stream says which run of the server a page talks to, and why a rebuild failed

Every connection to the rebuild stream SHALL open by naming the run of the server, and a page that
reconnects to another run SHALL reload. A rebuild that fails SHALL send its errors, and the page SHALL
show them over itself without reloading.

#### Scenario: The server restarts

- **WHEN** a page's stream reconnects to a run of the server with another name
- **THEN** the page reloads once

#### Scenario: A save that does not compile

- **WHEN** a rebuild fails
- **THEN** the page shows MSBuild's error lines in the development overlay and keeps running the code
  it has
