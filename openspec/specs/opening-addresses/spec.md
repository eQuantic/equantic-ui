# opening-addresses Specification

## Purpose
What a page's own code opens beside it: an address an action answered with, in a new tab or the
system's browser, and a file downloaded under a name, with the schemes a page may hand the browser.

## Requirements

### Requirement: A page's code opens an address beside the app

`Navigator.OpenAsync(address)` SHALL open the address beside the app and answer whether it did. The
browser SHALL open it in a new tab cut from its opener, resolving an address relative to the page, and
SHALL answer false when it blocks the tab. A Photon shell SHALL hand it to `IUrlLauncher` and SHALL
refuse a relative address, a path from the root included, with `ArgumentException`. Where nothing opens
addresses, a server's render, it SHALL answer false. An empty address SHALL throw `ArgumentException`.

#### Scenario: An address a server action answered with

- **WHEN** a button's handler awaits a server action that answers `/charts?ticket=1` and calls
  `Navigator.Of(context).OpenAsync` with it, in the dashboard in a browser
- **THEN** the browser opens `http://localhost:5299/charts?ticket=1` in a new tab, without a second press,
  and the call answers true

#### Scenario: On a Photon shell

- **WHEN** a handler opens `https://example.com/print/1` on a shell with a launcher
- **THEN** the launcher receives that address, and opening `/print/1` throws `ArgumentException` naming
  `address`

#### Scenario: The new tab cannot reach back

- **WHEN** the browser opens the tab
- **THEN** its `opener` is null

### Requirement: Only http and https addresses open in the browser

The browser's navigator SHALL open only `http` and `https` addresses, and SHALL answer false for any
other scheme without opening anything.

#### Scenario: An address that would run script

- **WHEN** a handler opens `javascript:alert(1)`, `data:text/html,…` or `file:///etc/passwd`
- **THEN** no tab opens and the call answers false

### Requirement: A page's code downloads a file under a name

`Navigator.DownloadAsync(address, fileName)` SHALL hand the file to the person and answer whether the
target took it. The browser SHALL download a file of the page's own origin under the name, through an
anchor it removes, and SHALL open another origin's file in a new tab. A Photon shell SHALL hand the
address to `IUrlLauncher`. An empty name SHALL throw `ArgumentException`.

#### Scenario: A catalogue of the page's own origin

- **WHEN** a handler downloads `/print/ticket-1.pdf` as `Catalogue.pdf`
- **THEN** the browser clicks an anchor with that address and `download="Catalogue.pdf"`, removes it, and
  opens no tab

#### Scenario: A file of another origin

- **WHEN** a handler downloads `https://files.example.com/c.pdf`
- **THEN** the browser opens it in a new tab and clicks no anchor
