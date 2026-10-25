## Context

A chosen theme crosses to the browser as data: the server writes `ThemeBridge.SerializeJson` into
`window.__EQ_THEME__`, and boot rebuilds it with `materializeTheme` before hydration. #1055 made a family a
typed value, a name and the files that ship it, and kept the wire as it was, a name, on the reasoning that
the browser sets text by name and the server declares the files. That holds for drawing and not for a
component, which reads the theme as a value on both sides.

eqc reads most calls through the bound tree (`BoundArguments.Of`), and three paths still read the syntax:
a vocabulary type's constructor, whose hand-written twin takes the C# defaults; the constructor of a twin
eqc writes, where no model binds; and an invocation. Each placed named arguments into one place a
parameter.

Android opens a typeface only from a file, so each shipped face is written to the app's cache once.

## Goals / Non-Goals

**Goals:** a family that is one value on both sides of hydration; every element a call lists reaching its
params parameter; a face cache that fails as a face that did not register.

**Non-Goals:** the generated theme's roles, which carry no family, mono or slant today on any theme the SDK
ships; the evaluation order of named arguments on the syntax paths, which the bound tree path already keeps.

## Decisions

- **The bridge carries the files.** Flutter has no hydration: its `ThemeData` is one object in one process.
  Here the theme is rebuilt from data, so the data carries the whole value, a family as its name and each
  file's assembly, resource, weights and slant, four values a file. A family with no files stays a string,
  so the common payload is byte for byte what it was.
- **A file's names are escaped, not vetted.** A family's name passes the face rule before it reaches the
  wire. A file's names are whatever an app called its files, so the bridge writes a `<` and a control
  character as their JSON escapes, which no HTML parser reads as a tag and every JSON reader reads back.
- **One placement for the syntax paths.** `BoundArguments.InPlaces` places each written argument, with a
  list for a params parameter: from that parameter's position on, every positional argument is one of its
  elements. The three paths share it and keep what differs between them, the defaults they fill and the
  dependencies a component's constructor drops.
- **The cache publishes by rename.** A face is written under a name of its own beside the face's and moved
  onto it, which the file system does in one step, so the name only ever holds a whole face, and checking
  a file's length or hash at every launch is not needed. The helper lives in the native framework, where
  it runs on every host, rather than in the Android shell, which has no runtime test here.
