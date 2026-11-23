## Why

#1761, #1764 and #1765. #1761 is a slice of #675 (a `TextEntry` cannot ask for an e-mail or phone
keyboard) under #195, the text-editing protocol of the Flutter parity epic (#190); #1764 is a sub-issue
of #190; #1765 is a sub-issue of #1587, the transpiler's fences.

An app that keeps typed values out of free text boxes has nothing to reach for. Dates and times have
`DatePicker` and `TimePicker`, but a `TextEntry` cannot say it takes an e-mail address, a telephone
number or a number, so a phone opens the text keyboard for each of them, and an amount of money is a
`TextInput` and the app's own parsing. A back office filled on a phone (prices in euros, kept as cents,
an order's phone number) met all of it at once.

## What Changes

- **`TextEntry.Kind`**, a `TextEntryKind`: `Text` (the default), `Email`, `Phone`, `Url`, `Number` and
  `Decimal`. On the web it is the `type`, `inputmode` and `autocomplete` that open each keyboard and
  offer autofill, with spelling and capitalization off for a value. On Photon a shell reads it from the
  `TextInputSession`: iOS's `keyboardType` and `textContentType`, Android's `inputType`, with
  autocorrection off for a value.
- **`TextInput` and `FormInput`** take `kind`, `prefix` and `suffix` in their constructors and
  factories. The prefix and the suffix are drawn beside the text in the muted colour (a currency's
  symbol, a unit), and announced with the field's name.
- **`MoneyInput`**: an amount in minor units (`long?`, cents for the euro) and a `Currency` (its ISO
  code, its symbol and its minor digits; `Currency.Euro` and five others are ready). It writes the
  amount as the culture writes a number, grouped while the field rests and plain while it is edited,
  puts the currency's symbol where the culture puts its own, reads what is typed in the culture and in
  the other decimal separator a phone's keypad may show, opens the decimal keypad, refuses more
  fraction digits than the currency has and an amount outside `min` and `max`, and hands back the
  amount in minor units, or null when the field is emptied.
- **`NumberInput`**: a whole number (`int?`) with its unit beside it, the digits keypad, `min` and
  `max`, the same states.
- **`NumberFormatInfo` crosses to the browser**: `NumberFormatInfo.CurrentInfo`,
  `NumberFormatInfo.InvariantInfo` and `CultureInfo.CurrentCulture.NumberFormat` (and the invariant
  culture's) read in the browser as the object the page's culture already travels as, and its scalar
  members by their own names. A write, a culture held in a variable and the group-size arrays stay
  refused (EQ2004).

## Capabilities

### New Capabilities

- `amount-inputs`: `MoneyInput`, `NumberInput`, `Currency`, and the prefix and suffix of a text input.

### Modified Capabilities

- `text-entry`: a kind, and what the web makes of it.
- `phone-keyboard`: the keyboard a shell opens follows the kind.
- `transpiler-bcl`: a culture's `NumberFormatInfo` is read in the browser.

## Impact

- **Public surface** (Primitives, Components): `TextEntryKind`, `TextEntry.Kind`, `Currency`,
  `MoneyInput`, `NumberInput`, and the factories. `TextInput`'s and `FormInput`'s constructors and
  factories widen with `kind`, `prefix` and `suffix` at the end, so no call that compiles today
  changes. The developer surface does not move.
- **eqc**: a strategy writes the culture's number format for the four reads, and `PlatformMemberRead`
  carries `NumberFormatInfo`'s scalar members.
- **Runtime**: `$eq.numberFormat`, the web lowering of the kind, the transpiled components.
- **Web realizer** and **Photon** (the host's session, the iOS and Android shells).
- **Served runtime**: the two components and the kind add bytes; a delta says what they bought.

## Principles

- 1 and 5: a developer names what the field takes (`TextEntryKind.Email`); each target derives its
  keyboard, autofill and spelling from it. No name borrows a target's word: `Kind`, not `InputMode`.
- 2: a back office's prices are one case; the capability is a field for any typed value.
- 4 and 10: written once (the components are C#, transpiled), realized and driven on both targets.
- 6: Flutter's `keyboardType` and `InputDecoration.prefixText`/`suffixText`, recorded in
  docs/FLUTTER-PARITY.md.
- 14: the currency's side is read from `NumberFormatInfo.CurrencyPositivePattern`, which failed the
  build (EQ2004). The gap is closed in eqc here (#1765) rather than worked around by reading the side
  from a formatted string.
