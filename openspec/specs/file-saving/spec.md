# file-saving Specification

## Purpose
TBD - created by archiving change a-page-offers-a-file-it-made. Update Purpose after archive.

## Requirements

### Requirement: A page offers a file it made through one contract

The SDK SHALL offer a page an `IFileSaver` capability that takes a file name and the content, as text
written in UTF-8 or as bytes, with an optional media type, and answers a `FileSaveOutcome`:
`Cancelled`, its default, when the person closed the panel, `Offered` when the target took the file
and saves it the way it saves downloads, and `Saved` when it was written where the person chose. When
the page states no media type, the SDK SHALL derive it from the name's extension, and SHALL use
`application/octet-stream` for an extension it does not know.

#### Scenario: A text file whose type the name gives

- **WHEN** a page saves `codes.txt` with the text `ABCD-1234` and no media type
- **THEN** the file offered holds the UTF-8 bytes of `ABCD-1234` and its media type is `text/plain`

#### Scenario: A file of an unknown kind

- **WHEN** a page saves `model.xyz` as bytes with no media type
- **THEN** the file offered is `application/octet-stream`

#### Scenario: A page that gave up before offering

- **WHEN** a page saves with a cancellation token already cancelled
- **THEN** every target throws `OperationCanceledException`, as .NET does, and offers nothing: the
  browser clicks no anchor and the desktop opens no panel

### Requirement: The browser downloads the file inside the press

On the web, saving SHALL create the content as a blob of its media type, click an anchor whose
`download` is the file name and whose address is the blob's object URL before anything is awaited, so
the person's gesture still holds, then revoke the URL and answer `Offered`.

#### Scenario: Saving from a button

- **WHEN** a button's press saves `codes.txt`
- **THEN** the browser receives a click on an anchor with `download="codes.txt"` and an object URL,
  the anchor is gone from the page afterwards, the URL is revoked, and the answer is `Offered`

### Requirement: The desktop saves where the person chooses

On the macOS and Windows shells, saving SHALL open the save panel on the file name, filtered to its
extension, and SHALL write the content to the path the person chose and answer `Saved`, or write
nothing and answer `Cancelled` when the person closes the panel.

#### Scenario: The person chooses a place

- **WHEN** a page saves `codes.txt` and the person chooses a path in the panel
- **THEN** that path holds exactly the bytes saved, and the answer is `Saved`

#### Scenario: The person cancels

- **WHEN** the person closes the panel without choosing
- **THEN** nothing is written and the answer is `Cancelled`

### Requirement: A target that cannot save says so

The server render SHALL register a saver whose `IsAvailable` is false and whose methods throw an
`InvalidOperationException` that says a file is offered to a person in their browser in answer to a
press, so a page that takes the saver still renders on the server and a call made there fails with
that reason rather than doing nothing. A shell that has not realized the capability SHALL register
none, so a component that requires it fails with the sentence naming the capability and the target.

#### Scenario: A page that takes the saver renders on the server

- **WHEN** a page whose constructor takes `IFileSaver` is rendered on the server
- **THEN** it renders, and the saver it was handed reports `IsAvailable` false

#### Scenario: A call on the server

- **WHEN** code rendering on the server calls `SaveTextAsync`
- **THEN** it throws `InvalidOperationException` whose message says the file is offered to a person
  in their browser, in answer to a press
