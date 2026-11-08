# served-assets Specification

## Purpose
TBD - created by archiving change what-the-server-hands-the-browser. Update Purpose after archive.

## Requirements

### Requirement: What an app serves is compressed with no line of the developer's

An app on `AddUI` SHALL answer its runtime and its modules in the encoding the browser accepts, Brotli or Gzip, and a
browser that accepts neither SHALL get the plain file. The developer SHALL write nothing for it.

#### Scenario: The runtime

- **WHEN** a browser asks for `/_equantic/runtime.js` with `Accept-Encoding: br`
- **THEN** the response says `Content-Encoding: br`, and decompresses to the bytes a plain request gets

#### Scenario: A module

- **WHEN** a browser asks for a page's module with `Accept-Encoding: gzip`
- **THEN** the response says `Content-Encoding: gzip`, and decompresses to the module

#### Scenario: No encoding

- **WHEN** a browser accepts no encoding
- **THEN** it gets the plain file
