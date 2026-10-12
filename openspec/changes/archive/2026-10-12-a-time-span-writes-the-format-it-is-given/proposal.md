# Proposal

Closes #837, a Bug under #565 (The transpiler's fences hold on every path, continued), found while
planning the provider family (#717): a `TimeSpan`'s provider cannot be translated until its format is.

## Why

Executed on both sides at d5097bc2, where 45 of the 50 cases of `TimeSpanFormatConformanceTests` fail
(each case runs in the invariant culture, pt-BR, de-DE, sv-SE and ar):

- A `TimeSpan` written with a format wrote its constant ("c") text whatever the format asked for. The
  formatter (`$eq.text.format`) handed a TimeSpan to the twin's own `toString`, which takes no format,
  and eqc lowered `ts.ToString(format)` to that `toString` directly. `ToString(@"hh\:mm")` of an hour
  and two minutes was `01:02:00` where .NET writes `01:02`, `$"{ts:g}"` and
  `string.Format("{0:G}", ts)` wrote the constant form, and a format .NET refuses wrote a value.
- `ts.ToString(format, provider)` crossed with its provider written as the name `CultureInfo`, which
  the runtime does not export, a ReferenceError on the page (#717).
- `ToString()` of a null `TimeSpan?` read it through null, where .NET writes nothing.
- Found in review, and older than this change: a provider named before its format
  (`ToString(provider: null, format: "g")`) was read as the format, for every value `ToStringStrategy`
  writes, so a number, a date and a TimeSpan wrote their text with no format. A null has no type, and
  the strategy asked the argument's type which one was the provider.

## What Changes

- The runtime gains `utils/timespan-text.ts`, .NET 10's `TimeSpanFormat`: `c`, `t` and `T` are the
  constant form in every culture, `g` (`[-][d:]h:mm:ss[.FFFFFFF]`) and `G` (`[-]d:hh:mm:ss.fffffff`)
  the general ones with the culture's decimal separator, and a longer format is drawn token by token
  without a sign (`d` up to eight, `h`, `m` and `s` up to two, `f` and `F` up to seven, quoted text, a
  backslash, a `%` before one letter). Any other format is .NET's FormatException in its words, an
  open quote included. The formatter hands a TimeSpan with a format to it, so `ToString`, a hole and a
  composite placeholder write the same text, and the twin's own `toString` writes its constant form
  through it.
- eqc hands a TimeSpan's `ToString` to the formatter, as it hands a date's: a format is read when the
  call runs, the invariant culture is the formatter's invariant flag, the current culture and a null
  provider are the call with none, and any other provider fails the build (EQ2108). A null `TimeSpan?`
  writes nothing.
- eqc's `ToStringStrategy` asks which argument is the provider of the parameter it binds to, by its
  name or its place, and the argument's own type only where no method is bound.

For a developer: `duration.ToString(@"hh\:mm")` and `$"{duration:g}"` write in the browser what they
write on the server.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `runtime-dates`: a TimeSpan writes the format it is given.

## Impact

- eqc: `TimeSpanStrategy` leaves `ToString` to `ToStringStrategy`, which reads a named provider by its
  parameter for every value it writes. No public surface moves.
- Runtime: the new `utils/timespan-text.ts`, read by `format.ts` and by the TimeSpan twin. The served
  runtime grows by the writer.
- Not reached: the web realizer, Photon, the SDKs, the templates and the shared components, whose
  pinned modules do not change. The developer surface does not move.
- #715's null-argument baseline only shrinks, by one: `TimeSpan.ToString(String, IFormatProvider)`
  answers as .NET's.
- Left where they are: reading a TimeSpan through a format (`ParseExact`) and the provider of the
  time types' `Parse` and `TryParse` (#717, #806).
