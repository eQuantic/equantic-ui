# Spec Delta

## ADDED Requirements

### Requirement: The document takes its surface, its text colour and its face from the theme

On the web the theme's stylesheet SHALL set the document's background to the theme's `Background`, its text colour to `TextPrimary` and its face to `TextFamily` (the platform's own face when the theme names none), with no margin around the page and with antialiased text. Photon SHALL clear its window to `Background`. A page whose app chose no theme SHALL be written with Photon's theme.

#### Scenario: A brand theme

- **WHEN** an app's theme answers a cream `Background`, a brown `TextPrimary` and `TextFamily` "Atkinson Hyperlegible Next"
- **THEN** the page's stylesheet sets the body's background and colour from those two and its `font-family` to that face before the platform's, and Photon clears its window to the cream

#### Scenario: No theme chosen

- **WHEN** an app calls `AddUI` without `UseTheme`
- **THEN** the page carries Photon's theme stylesheet, and every `var(--eq-color-*)` the lowered markup names is defined

### Requirement: The theme names the selection, the placeholder and the scrollbars

`IAppTheme` SHALL carry `Selection`, `OnSelection`, `Placeholder`, `ScrollbarThumb` and `ScrollbarTrack`, defaulting to `FocusRing` at 28%, the text's own colour, `TextMuted`, `TextMuted` at 55% and `SurfaceSubtle`. Every target SHALL draw what it draws of them from the slots:

- the web, the document's `::selection`, every field's placeholder and, when the theme sets the
  scrollbar slots, the page's scrollbars (with the defaults the page keeps the platform's own, so an
  app's own scrollbar CSS still applies);
- both targets, a `TextEntry`'s placeholder, a table's scrollbars and the code editor's selection: its
  band in `Selection`, and the text it covers in `OnSelection` when the theme names one, so a band a
  theme draws opaque does not hide the text under it.

#### Scenario: The defaults change nothing on Photon

- **WHEN** a theme sets none of the five
- **THEN** Photon draws a field's placeholder, a table's scrollbars and the code editor's selection exactly as before

#### Scenario: A theme's own selection

- **WHEN** a theme answers `Selection` as its brand green and `OnSelection` as white
- **THEN** the web writes `::selection` with that background and that colour, and on both targets the code editor's selection band takes the green and the text it covers the white, while the rest of the line keeps its colours

#### Scenario: A theme's own scrollbars

- **WHEN** a theme answers `ScrollbarThumb` and `ScrollbarTrack`
- **THEN** the web writes them as the page's `scrollbar-color`, and a table's scrollbars take them on both targets

### Requirement: An app's own CSS comes after the SDK's

The head SHALL hold the framework's invariants, then the theme's stylesheet, then the CSS the app adds with `HtmlShellOptions.AddStyles`, then the app's head tags, so an app's rule wins every tie with the SDK's. `AddStyles` SHALL append, and nothing an app writes SHALL replace the SDK's rules.

#### Scenario: An app overrides the body

- **WHEN** an app adds `body { background: white; }` with `AddStyles`
- **THEN** the rule comes after the theme's stylesheet in the head, and the body is white

### Requirement: A page the server does not render shows a placeholder the SDK styles

Until its module mounts, a page the server does not render SHALL show the SDK's placeholder, written
with the class `eq-loading` by both the shell and `boot.ts`, and the theme's stylesheet SHALL centre it
in the window, so no app writes CSS for it.

#### Scenario: A client-only page

- **WHEN** a visitor asks for a page marked `DisableSsr`
- **THEN** the page holds `<div class="eq-loading">`, and the head holds the theme's `.eq-loading` rule
