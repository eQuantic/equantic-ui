# Proposal

Closes #1022, a Feature under #898 (1.0: the web SDK stable, with the native track experimental).

## Why

A page cannot hand the person a file it made. eQuantic.Auth shows a set of recovery codes once and
wants a "Download .txt" beside "Copy all" (eQuantic/equantic-auth#277): the codes exist only in that
page, so there is no URL a server could serve them from afterwards. `Link` carries no download,
top-level navigation to a `data:` URL is blocked in current browsers, and `IFileDialogs` is a desktop
contract for asking a person for a path. `ITextClipboard` covers "Copy all", and nothing covers the
download. Without a contract a developer would reach for the `HtmlElement` escape hatch and write the
browser's object-URL dance by hand, which the product principle rules out.

Flutter has no core answer either: its ecosystem's `file_selector` builds an `XFile` from bytes, a
name and a media type and saves it, as a download in the browser and through a save panel on the
desktop. This proposal takes the same shape, and the same split by target.

## What Changes

- **`IFileSaver`** (vocabulary, `eQuantic.UI.Primitives`), the save half of `IFilePicker`:
  - `IsAvailable`, whether this target can offer a file to the person at all.
  - `SaveTextAsync(name, text, mimeType = null, cancellationToken)`, the text written as UTF-8.
  - `SaveBytesAsync(name, content, mimeType = null, cancellationToken)`, for a file the page holds as
    bytes.
  - The media type is derived from the name's extension when the page states none (`codes.txt` is
    `text/plain`, `report.csv` is `text/csv`, an unknown one is `application/octet-stream`), so the
    developer writes a file name and nothing else.
  - The answer is a **`FileSaveOutcome`**: `Cancelled`, its default, when the person closed the
    panel, `Offered` when the target took the file and saves it the way it saves downloads (the
    browser, which tells a page nothing about where it went or whether the person kept it), and
    `Saved` when it was written where the person chose (a desktop save panel). A person cancelling has
    not failed at anything, so it is an answer and not an exception; a PAGE that gave up, through its
    cancellation token, meets `OperationCanceledException` before anything is offered, as .NET does,
    on every target.
- **The web** registers `IFileSaver` in the runtime: a `Blob` of the content under its media type, an
  object URL, and an anchor with `download` clicked inside the press that asked, then the URL revoked.
  Nothing is awaited before the click, so the person's gesture still holds when the browser decides.
- **The desktop shells (macOS, Windows)** register one realization over the `IFileDialogs` they
  already have: the save panel opens on the suggested name with a filter for its extension, and the
  bytes are written to the path the person chose.
- **The phones** answer the contract the way every capability a shell has not realized answers: the
  shells register none, so a component that requires it fails with `CapabilityScope.Require`'s
  sentence naming the capability and the target, and one that declares it nullable gets null. Saving
  on a phone is the export picker (`UIDocumentPickerViewController` exporting, Android's
  `ACTION_CREATE_DOCUMENT`), which #782 (D15, picking a document on a phone, under Track D #774)
  already names as in its scope, beside the document picker that shares its plumbing. It is realized
  there, not here.
- **The server render** registers an absent saver whose `IsAvailable` is false and whose two methods
  throw `InvalidOperationException` with a sentence that says why: a file is offered to a person in
  their browser, in answer to a press, and a page rendering on the server has no person to offer it
  to. A page that takes the saver still renders there; only a call made outside a press fails, loudly.
- **A sample**: the dashboard sample gains a button that saves a text file the page made, and a real
  browser shows the download.

## Impact

- Reaches the vocabulary (`IFileSaver`, `FileSaveOutcome`), the runtime (a `devices/file-saver.ts`
  realization registered beside the picker, the enum's twin), the server's absent capabilities, the
  macOS and Windows shells' capability registrations (one shared realization in
  `eQuantic.UI.Native.Hosting`), the dashboard sample and the wiki's Capabilities page in its five
  languages. eqc, the SDKs and the templates do not change.
- The public surface grows (contract): the interface and the enum, in `PublicAPI.Unshipped.txt` of
  Primitives, and the shared realization in the experimental native track. The developer surface
  outside C# (`developer-surface.baseline.txt`) does not move: no setting, no MSBuild property.
- No break.

## Capabilities

### New Capabilities

- `file-saving`: a page offers a file it made for the person to save, through one C# contract on every
  target, each target answering what saving means there or saying it cannot.

### Modified Capabilities

(none)
