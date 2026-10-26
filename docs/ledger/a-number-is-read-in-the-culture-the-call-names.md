- **2026-10-25 · A number is read in the culture the call names**: `decimal.TryParse(text,
  NumberStyles.Float, CultureInfo.CurrentCulture, out var d)` failed the build (EQ2108), and a number
  read with no provider read the invariant culture in the browser under a warning (EQ2110), where .NET
  reads the current one; the spreadsheet's status bar met it summing `7,01` on a pt-BR page. The
  runtime's number grammar, .NET's `TryParseNumber` with the invariant culture's symbols written in,
  takes the culture's `NumberFormatInfo` and matches it as .NET 10's source does (a no-break space read
  from a typed space, a hyphen for a culture whose minus is U+2212, the currency's separators before the
  number's, a real's infinity and NaN in the culture's words). eqc asks the date readers' policy: no
  provider, the current culture and a null one, a null behind a cast included, read the page's culture,
  the invariant culture passes a flag, any other is EQ2108; `ParseCulture` and EQ2110 are gone. Proved by
  eight cases in nine cultures run on both sides
  ([#1087](https://github.com/eQuantic/equantic-ui/issues/1087),
  [#908](https://github.com/eQuantic/equantic-ui/issues/908)). Proposed and archived through OpenSpec
  with slice 2a (`openspec/specs/transpiler-bcl`).
