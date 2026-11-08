# served-assets Specification

## Purpose
How an app on `AddUI` hands the browser what every page loads, its runtime and its modules: in the
encoding the browser accepts, with no line of the developer's, and nothing that answers one user compressed.

## Requirements

### Requirement: What an app serves is compressed with no line of the developer's

An app on `AddUI` SHALL answer its runtime and its modules in the encoding the browser accepts, Brotli or Gzip, and a
browser that accepts neither SHALL get the plain file. The developer SHALL write nothing for it. Only what every user receives alike SHALL be compressed: a page's HTML and an action's JSON
SHALL go out as they are.

#### Scenario: The runtime

- **WHEN** a browser asks for `/_equantic/runtime.js` with `Accept-Encoding: br`
- **THEN** the response says `Content-Encoding: br`, and decompresses to the bytes a plain request gets

#### Scenario: A module

- **WHEN** a browser asks for a page's module with `Accept-Encoding: gzip`
- **THEN** the response says `Content-Encoding: gzip`, and decompresses to the module

#### Scenario: No encoding

- **WHEN** a browser accepts no encoding
- **THEN** it gets the plain file

#### Scenario: What answers one user

- **WHEN** a browser asks for a page's HTML or a JSON answer with `Accept-Encoding: br`
- **THEN** the response carries no `Content-Encoding`, so a secret beside text the request put there is never
  measured through a compressed length
