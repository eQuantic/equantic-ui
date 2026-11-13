# Proposal

Closes #1281, a sub-issue of #1277 (eqc compiles only what a save changed), under #900.

## Why

The last of the seven slices that bring the web's edit-to-view under a second (#900). With the
compilation kept warm (#1278) and a module written only when its text changed (#1279), a save still
transpiles every file of the app: on the dashboard sample, 132 files and 3.1 to 3.7s of a served
compile at a load average of 50 to 60, against 0.1s to keep the compilation and 0.15s of bun. A save
that changes the text inside one screen's `Build` can change no other file's module but the few that
read that screen's code.

## What Changes

- A served eqc compiles again only the files a save can have changed: the files whose text changed,
  every file whose last compile read one of them, and every file whose last compile failed. Every
  other file's modules, warnings and resource uses are taken from the build before, as they were
  compiled.
- A save that changes anything outside a body (a declaration, a signature, an attribute, a constant,
  an initializer, a using, a comment between members) compiles every file, as today: a declaration can
  change what another file's code binds to, which no record of the last build can see.
- What a compile read is recorded as it reads it. Every way the compiler reaches the syntax of a
  declaration goes through one accessor that notes the file, and the two caches that keep an answer
  read from another file's code hand on what they read to every compile that takes the answer. A guard
  fails the build of a test run when code of the compiler reaches a declaration's syntax another way.
- Nothing a developer writes changes, and a build that is not served compiles every file as it always
  has. The parts reached are eqc's library (`eQuantic.UI.Compiler`) and its served loop. The
  compiler's surface, a tool's, moves only where `AppBuild` already says what it compiled. The
  developer surface does not move.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `hot-reload`: a served eqc compiles again only the files a save can have changed.
