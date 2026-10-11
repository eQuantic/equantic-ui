# Design

## Context

.NET writes a number or a date from the current culture's `NumberFormatInfo` and
`DateTimeFormatInfo`, wherever C# turns it into text. The browser's formatter (`utils/format.ts`)
wrote the standard specifiers through `Intl` and the culture the page had installed, which it had only
when the app had a string catalog, whose build-time "format facts" (a currency code, the date
patterns) came from the build machine's ICU for the UI culture. Every other path, a concatenation, an
interpolation hole, `ToString()` with no specifier, wrote JavaScript's own invariant text. The three
other date types printed through their twins.

## Goals / Non-Goals

**Goals:**

- One source for how a culture writes a number and a date: .NET's own data, the data the server
  rendered the page with.
- Every path C# writes a number or a date through writes it as .NET does, in the culture in force.
- A page that has installed no culture is in the invariant culture, on every host.

**Non-Goals:**

- The culture a server action runs in (#580), which is the request's, and its own change.
- A `DateTimeOffset`'s local time (`Now`, `LocalDateTime`, `ToLocalTime`, #626), a time zone matter
  rather than a culture's, and its own change.
- A non-Gregorian calendar as a culture's default: th-TH counts Buddhist years and ar-SA the Um al-Qura
  calendar in .NET, and the browser writes the Gregorian date for both. Digits are not this case: .NET
  writes ASCII digits in every culture, and so does the browser, which draws every number by hand.

## Decisions

### The format culture is .NET's data, shipped with every page

The server writes `__EQ_CULTURE__.format` on every page: the number symbols (the separators and group
sizes of a number, a currency and a percent, the signs, the currency, percent and per mille symbols, the
words for NaN and the infinities), the default digits and the patterns of `N`, `C` and `P`, and the date
patterns, separators, era, first day of the week, day and month names (genitive ones included) and AM
and PM designators, serialized from the request's format culture by `CultureFormatBridge`. A culture switch fetches the same document for the culture it switches to
(`/_equantic/culture/{name}.json`). This is what Flutter's `intl` does: a locale's `NumberSymbols`
and `DateSymbols` are data the app carries, never the platform's.

Alternatives considered:

- Derive everything from `Intl` in the browser. Rejected: the browser's ICU and .NET's disagree where
  it shows (`ar`'s minus sign carries a left-to-right mark `Intl` keeps outside the sign, `ar-SA`'s per
  mille is `؉` where `Intl` has none, de-DE abbreviates September `Sept.` in .NET and `Sep` in a JS
  runtime), and a page must print what its server rendered.
- Ship a table of cultures in the runtime. Rejected: every page would pay for every culture, and the
  table would be a second copy of data .NET already has.
- Keep the facts in the string catalogs. Rejected: they travelled only with a catalog, from the build
  machine's ICU, and for the UI culture where formatting reads the format culture. The catalogs carry
  strings only now.

### One copy of a culture's names

A calendar's first day and its day and month names are the format data's `DateTimeFormatInfo` (the
twin of `CalendarNames` reads them there), where the server wrote them a second time as a calendar
catalog beside the format data: two copies of one fact on every page, read by two modules. With no
culture installed, a calendar is the invariant culture's as a date is: it followed the host's locale,
and named the days of a calendar in Portuguese beside an invariant date on a Portuguese machine.

### `N`, `F`, `C` and `P` are laid out from the data

The four specifiers people write most are drawn as .NET's `Number.Formatting` draws them: the value (a
percent's times 100) rounded to the precision written or to the culture's own digits, by the type's tie
rule, its whole part grouped by the specifier's group sizes, and set in .NET's pattern table at the
culture's index, `#` for the number, `-` for its negative sign, `$` and `%` for its symbols. `Intl` stays
for a culture whose data did not travel, where nothing names its currency, so `C` writes the generic ¤
there in the invariant culture's patterns.

Alternatives considered:

- `Intl` with `numberingSystem: 'latn'`. Rejected: ASCII digits, but ar-EG's separators then become
  Latin ones, the space of a currency and a percent stays a no-break one, and the default digits stay
  `Intl`'s, where .NET reads 3 for `N` on ICU and 2 on NLS. Only the data says what the server wrote.

### A page with no culture installed is in the invariant culture

`activeFormatLocale()` and the formatter's data answer the invariant culture when nothing is
installed, never the host's locale. Boot installs the request's culture before hydration on every
page, so that state is only a test's or a playground's, and both sides of a conformance case run in
the culture the case names, the invariant one when it names none.

### Every number in text goes through the formatter

`StringConversion` writes a number or a date of a culture-sensitive type through `$eq.text.format` with
no specifier: a double, a float, a decimal, a signed integer, a native one, and the date types. An
unsigned integer, a char and a `TimeSpan` read the same in every culture and are left as they are, and
so is a non-negative integer constant, whose digits are its text. The same path serves a
concatenation, a plain and an aligned interpolation hole, `ToString()`, `Convert.ToString`,
`string.Join`, the value a `StringBuilder` appends or inserts, and each member of a record's text, which
.NET's PrintMembers writes as a concatenation would.

### The compiler tells the formatter what a number is

A number crosses as a JavaScript number, so the kind travels with the call (`FormatKind`): a float, a
double, every integer width, and `nint` and `nuint` as the platform's width (64 bits, as on the hosts
.NET serves from). `D`, `X` and `B` refuse a double whatever it holds, as .NET does. With no specifier
the kind of every number but a double travels, a double being how the formatter reads a number it is
told nothing about: a float writes its own digits, and an integer writes no sign on a zero, which
JavaScript makes of `-1 / 2`. A value whose kind nothing passes (an `object`) is formatted by what it
holds, as before.

### The date types print through the formatter

`DateOnly`, `TimeOnly` and `DateTimeOffset` reach `format` as a `DateTime` does, each telling it its
type: a `DateOnly` takes the date specifiers and is its culture's `d` with none, a `TimeOnly` the time
ones and `t`, a `DateTimeOffset` all of them with its offset, `o` and `K` writing it and `u` and `R`
converting by it. A provider is read as .NET reads it (`NamedCulture`). A custom picture writes the
culture's separators for `/` and `:`, the offset for `z`, `zz` and `zzz` (the host's for a `DateTime` of
no kind), and the era for `g` and `gg`.

### The SDK writes a machine's text in the invariant culture

Text a parser reads is not text for a person. The Mermaid layout's curve paths and view boxes are
written with `string.Format(CultureInfo.InvariantCulture, …)`, which both sides support. Its
coordinates are whole and positive by construction, the nodes' margins keeping every curve inside the
first quadrant, so concatenation wrote the same digits in every culture, and no test through the
layout can reach the case it guards: a negative coordinate, which sv-SE writes with U+2212 and no path
parser reads, on the server and in a native shell as much as in the browser. It is the idiom, written
where the SDK writes a machine's text, rather than a defect a page showed.

## Risks / Trade-offs

- [Every page carries its culture's format data] → measured before compression, 1.2 KB of JSON for
  en-US and 3 KB for ar-EG, whose names the encoder escapes, with the calendar catalog it replaces gone;
  the served runtime grows by the formatter's code for the date types, the symbols and the patterns.
- [A component that built a key or a style from a number now writes the culture's text, as .NET
  does] → the SDK's own components were read for it: their keys hold non-negative integers, whose
  digits every culture writes alike, their labels are a person's text and follow the culture as the
  server's already did, and only the Mermaid layout wrote a machine's text. An app's code behaves as its
  C# does on the server.
- [EQ2109 retired] → a build that relied on it to refuse `ToString(CultureInfo.CurrentCulture)` now
  compiles it, and it prints what .NET prints.
