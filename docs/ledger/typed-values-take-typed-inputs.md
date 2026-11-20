- **2026-11-20 · Typed values take typed inputs**: a `TextEntry` says what kind of value it takes (`Email`,
  `Phone`, `Url`, `Number`, `Decimal`) and each target opens that keyboard and offers its autofill: `inputmode`
  and `autocomplete` on the web, the keyboard and content type on iOS, the input type on Android. `TextInput`
  and `FormInput` take the kind and a prefix and a suffix beside the text. `MoneyInput` takes an amount in a
  currency's minor units and writes and reads it in the culture, the symbol where the culture puts its own and
  the other decimal separator a phone's keypad may show read too; `NumberInput` takes a whole number with its
  unit. A culture's `NumberFormatInfo` crosses to the browser, and `decimal.Truncate`, `Floor` and `Ceiling` with
  it ([#1761](https://github.com/eQuantic/equantic-ui/issues/1761),
  [#1764](https://github.com/eQuantic/equantic-ui/issues/1764),
  [#1765](https://github.com/eQuantic/equantic-ui/issues/1765)).
