## ADDED Requirements

### Requirement: An app states its icon once, written like any file of the app

An app's icon SHALL be an `IAppIcon` in `Assets/AppIcon.cs`, or a designer's `Assets/AppIcon.png`.
`IAppIcon.Build` SHALL take the use it is drawn for, `AppIconUse.Launcher` or `AppIconUse.Small`. The
icon SHALL compile with the `Using` items the app compiles with, each SDK handing them to the icon tool,
less the ones the vocabulary cannot resolve, and with the app's vector catalog where the SDK writes
one, so an icon may draw the app's own SVG artwork.

#### Scenario: An icon drawing the app's artwork

- **WHEN** an app's `AppIcon.cs` writes `Drawing(Vectors.Launcher, SizeValue.Fill)` with the factory the app's global using brings
- **THEN** the build compiles it and draws the artwork, where it failed with CS1955 and CS0103

#### Scenario: A web host's usings

- **WHEN** the app's usings name ASP.NET namespaces the icon is not compiled against
- **THEN** those usings are dropped and the icon compiles

### Requirement: An icon's vectors are drawn on every machine alike

The build of an app icon SHALL draw every vector of its tree (`Drawing`, `Icon`) with a rasterizer in
plain C#, on any operating system, filling by the nonzero rule and stroking with round caps and joins,
so that an icon built in a Linux container is the one drawn on a laptop.

#### Scenario: An icon built where no platform rasterizer exists

- **WHEN** an icon drawing an SVG is built on a machine with no platform icon rasterizer
- **THEN** the artwork is drawn, where it was the engine's placeholder box

### Requirement: The web serves the tab's icon at the root

The web set SHALL hold `favicon.ico` with 16, 32 and 48 from the icon's small cut, its transparency
kept, or from the icon when the app draws one artwork. Every page SHALL link `/favicon.ico` whenever the root answers one, with the
built file's sizes when the built file answers it, and the server SHALL answer `/favicon.ico` from the app's own `favicon.ico` when its web root has one, a referenced
project's static web asset included, from the built file otherwise, and with 404 when there is neither.

#### Scenario: A browser asks for the favicon

- **WHEN** a browser requests `/favicon.ico` from an app that declares an icon
- **THEN** it receives the built `favicon.ico` as `image/x-icon`

#### Scenario: An app with its own file

- **WHEN** the app has `wwwroot/favicon.ico`
- **THEN** `/favicon.ico` answers that file, and every page links it without sizes

#### Scenario: A transparent cut reduced

- **WHEN** the small cut's transparent edge is reduced to 16 pixels
- **THEN** its colour is weighed by its coverage and no dark rim appears
