# Spec Delta

## ADDED Requirements

### Requirement: A frame of the app is named by its C#

The development error overlay SHALL name a frame of the app's own code by the C# file and line its
module's map leads to. A frame of an app module whose map cannot be read SHALL be named by the module,
the C# type the module was written from, and SHALL carry no JavaScript position.

#### Scenario: A page's handler throws

- **WHEN** a page's C# event handler throws, in a Debug build, with the overlay installed
- **THEN** the call stack names the handler's C# file and line, and the source context shows the C#
  around it

#### Scenario: A module with no map

- **WHEN** a frame's URL is an app module under `/_equantic/` whose map answers 404
- **THEN** the frame is named by the module's name, without the `.js` extension, the hash bun gives a
  chunk, or a line and a column

### Requirement: A frame inside the SDK is named as the SDK's

A frame whose URL is the served runtime's SHALL be named as the SDK's. Where the runtime's map leads
it to C# the SDK transpiled into the runtime, it SHALL be named by that C# file and line, from the
repository's `src/` folder; where it leads to the hand-written runtime, by the runtime module's name;
where no map can be read, as the eQuantic.UI runtime with no position. The served runtime's map SHALL
lead each transpiled module's code to its C#, SHALL carry no source text, and SHALL be answered at
`/_equantic/runtime.js.map` in Development alone.

#### Scenario: A position inside a transpiled component

- **WHEN** the served runtime's map is read at a position of the code `SheetController.ts` holds
- **THEN** it leads to `eQuantic.UI.Spreadsheets/SheetController.cs`, at the line of that C#

#### Scenario: A position inside the hand-written runtime

- **WHEN** a frame of the runtime's URL maps to `eQuantic.UI.Runtime/src/shared/lowering.ts`
- **THEN** the frame is named `eQuantic.UI runtime › shared/lowering`, and nothing names a `.js` file

#### Scenario: The map is the developer's alone

- **WHEN** `/_equantic/runtime.js.map` is asked of a server outside Development
- **THEN** it answers 404

### Requirement: An error raised inside the SDK says the fault is the SDK's

When the frame that raised an error is the SDK's, the overlay SHALL say the fault is in eQuantic.UI.
When no frame of the app is on the stack, it SHALL say the app's code is not involved; when one is, it
SHALL name the first such frame as the call to check. It SHALL offer a report to copy, holding the
message, the SDK's version, the page's path, the browser and every frame named as the overlay names
it.

#### Scenario: A click inside a spreadsheet throws

- **WHEN** an exception is raised by the runtime's handler of a click, with no frame of the app on the
  stack
- **THEN** the overlay says the fault is in eQuantic.UI and not in the app, and its report names the
  SDK's version and every frame by the SDK's file or module

#### Scenario: The SDK throws inside a call the app made

- **WHEN** the throwing frame is the SDK's and a later frame is the app's `Pages/Home.cs:12`
- **THEN** the overlay names `Pages/Home.cs:12` as the call to check first

### Requirement: The call stack is never shown as JavaScript

The overlay SHALL show the call stack only once its frames are named, and SHALL name every frame, a
mapped one or not, by its owner: no frame SHALL be shown at a JavaScript file's line and column, while
the maps are read or after reading one failed.

#### Scenario: Every map fails

- **WHEN** an error is shown and every map fetch fails
- **THEN** the call stack names the SDK's frames as the eQuantic.UI runtime and the app's by their
  modules, and holds no `.js:` position

### Requirement: The overlay is titled by what was raised

An exception raised at run time SHALL be titled Unhandled Runtime Error. An exception an asynchronous
method raised that nothing awaited SHALL say so, in place of a promise rejection.

#### Scenario: An uncaught exception

- **WHEN** a handler throws and nothing catches it
- **THEN** the overlay's title is Unhandled Runtime Error, and nowhere says Build Error

#### Scenario: An asynchronous method nobody awaited

- **WHEN** an asynchronous method throws and nothing awaited it
- **THEN** the overlay says the exception was raised by an asynchronous call nothing awaited, and its
  message names no promise

### Requirement: The ResizeObserver loop notice opens nothing

The browser's notice that a ResizeObserver loop left notifications undelivered SHALL NOT open the
overlay; it carries no exception and reports no fault of the app or the SDK.

#### Scenario: The window is resized

- **WHEN** the browser reports `ResizeObserver loop completed with undelivered notifications` as an
  error event with no error
- **THEN** the overlay does not open
