# build-analyzers Specification

## Purpose
What the SDK's Roslyn analyzers report in an app's code, in every build and in the editor as it is
typed: the mistakes that compile, render on the server and fail only where a page is built or read in
another language. Both SDKs hand them to an app, the web one and the native one.

## Requirements

### Requirement: A constant outside the values its parameter allows is a build error

The analyzer the web SDK's generator and the native SDK's generator both carry SHALL report EQ3106, an
error, where a call hands a compile-time constant to a parameter marked
`System.ComponentModel.DataAnnotations.AllowedValuesAttribute` and the constant is none of the values
the mark lists. Numbers SHALL compare by their value, whatever type each is written in. The message
SHALL name the value, the parameter, its method, the values it takes, and the mark's `ErrorMessage`
when it has one. A value known only when the code runs, a default the parameter writes for itself and
a `params` array SHALL NOT be reported. `Icon`'s constructor and the `Icon` factory SHALL mark `size`
with the four steps of `IconSize`, and every factory parameter that mirrors a constructor parameter
SHALL carry the same mark and words.

#### Scenario: An icon off the scale

- **WHEN** an app's code writes `Icon(Icons.Search, 22)` or `new Icon(Icons.Search, 22)`
- **THEN** the build fails with EQ3106, naming 22, `size`, and the sizes 16, 20, 24 or 32

#### Scenario: A step of the scale, written any way

- **WHEN** the size is `20`, `16`, `(float)32d`, `IconSize.Lg`, omitted, or a variable
- **THEN** nothing is reported

#### Scenario: An app's own mark

- **WHEN** an app declares `Mode([AllowedValues("grid", "list")] string mode)` and calls `Mode("table")`
- **THEN** the build fails with EQ3106: `"table" is not a value 'mode' of Mode(…) allows: it takes "grid" or "list".`

#### Scenario: An app's component, through its generated factory

- **WHEN** an app's component takes `[AllowedValues(1, Spans.Wide)] int span` and a screen calls `Tile(span: 3)`
- **THEN** the generated factory's parameter carries the mark with its values written as literals, and the
  build fails with EQ3106

#### Scenario: A mark of ints on a double

- **WHEN** a parameter `[AllowedValues(1, 2)] double zoom` is handed `2` and `3.0`
- **THEN** only `3` is reported

### Requirement: A resx accessor read once into a static is a warning

The same analyzer SHALL report EQ3107, a warning, where a resx catalog's accessor (the shape Visual
Studio's `ResXFileCodeGenerator` writes, recognised as eqc recognises it) is read in a static field's
initializer, a static property's initializer or a static constructor, and SHALL name the accessor and
what runs once. A read in a getter, a method, a lambda, a local function or an instance's field SHALL
NOT be reported, nor a static property of a class that is not a catalog.

#### Scenario: A static field

- **WHEN** an app writes `static readonly string Heading = Strings.Title;`
- **THEN** the build warns EQ3107: `'Strings.Title' is read once, when the static 'Heading' is initialized, …`

#### Scenario: A getter

- **WHEN** an app writes `static string Heading => Strings.Title;`
- **THEN** nothing is reported

#### Scenario: Every accessor of a collection

- **WHEN** an app writes `static readonly string[] Lines = [Strings.Title, Strings.Subtitle];`
- **THEN** each of the two accessors is reported
