## Context

A `TextEntry` lowers to a chrome-less `<input>` on the web and to Photon's own field, whose phone
keyboard a shell opens from the host's `TextInputSession` (#770). The culture a page formats in travels
with it as .NET's own `NumberFormatInfo`, under .NET's names (`CultureFormatBridge`, `utils/culture.ts`),
and the formatter reads it.

## Decisions

### A kind, not a keyboard

Flutter names it `keyboardType`, and #675 proposed `TextKeyboard`. A desktop has no keyboard to choose,
and the kind decides more than a keyboard: the autofill a browser or a platform offers, and whether the
text is prose (spelling, autocorrection, capitalization). So the field says what it TAKES
(`TextEntryKind`), and each target derives the rest. `Obscure` stays apart: a PIN is a `Number` that
hides what is typed.

### The web: `inputmode` over `type`, but for the telephone

| Kind | `type` | `inputmode` | `autocomplete` |
|---|---|---|---|
| `Text` | `text` | | |
| `Email` | `text` | `email` | `email` |
| `Phone` | `tel` | | `tel` |
| `Url` | `text` | `url` | `url` |
| `Number` | `text` | `numeric` | |
| `Decimal` | `text` | `decimal` | |

Every kind but `Text` also says `spellcheck="false"` and `autocapitalize="none"`: a value is not prose.
`type="email"` and `type="number"` do not support the selection API (`setSelectionRange` throws), which
the runtime calls to put an autofocused entry's caret at its end and to select on focus, and
`type="number"` writes and reads only the invariant decimal point and changes on a wheel. `inputmode`
opens the same keyboards with none of that (measured in the browser, see tasks). `type="tel"` supports
selection and is the one every autofill heuristic and keyboard knows. A multi-line entry is prose: the
kind applies to a single line.

### Photon: the shells read the kind from the session

`TextInputSession` already tells a shell whether the field hides its text, and starts the keyboard over
when that changes; the kind travels the same way. iOS: `UIKeyboardType` `EmailAddress`, `PhonePad`,
`Url`, `NumberPad`, `DecimalPad`, and `UITextContentType` for the three autofill kinds. Android:
`ClassText | TextVariationEmailAddress`, `ClassPhone`, `ClassText | TextVariationUri`, `ClassNumber`,
`ClassNumber | NumberFlagDecimal`. A desktop host has no keyboard to choose. Android's autofill hints
need the autofill framework's view structure on Photon's view, which is #1762's.

### The currency's symbol is an affix, not part of the text

Flutter's answer is `InputDecoration.prefixText` and `suffixText`, beside an `intl` formatter. Writing
the symbol into the field's text would reformat under the caret and make the text something the
reader has to type around. So `TextInput` gains a prefix and a suffix drawn beside the entry, and
`MoneyInput` hands the currency's symbol to the side the culture puts its own:
`CurrencyPositivePattern` 0 (`$n`) and 2 (`$ n`) before, 1 and 3 after. pt-PT reads `12,50 €`, en and
nl-NL `€ 12.50` and `€ 12,50`. The affixes are strings, as `Stepper.Suffix` is: they are part of the
value, and the entry's accessible name carries them (`Price (€)`), since a screen reader on the field
reads its name and its value and not the text beside it.

That needs the culture's pattern in the browser, which EQ2004 refused. The structural fix is that the
browser HAS the object: `NumberFormatInfo.CurrentInfo` writes `$eq.numberFormat()`, which answers the
culture's data, the invariant culture's in an invariant read, and for a culture whose data did not travel
the separators `Intl` gives with the invariant culture's patterns, as the formatter already does for
`C`. Its scalar members are carried by their names. Not carried: the group-size arrays (the browser
would hand out the shared array .NET copies) and every write (.NET's current culture is read-only
there).

### Reading an amount

The field holds the plain number while it is edited (`F` with the currency's digits) and the grouped
one while it rests (`N`), as a spreadsheet does, so an edit never meets a group separator. What is typed
is read, in order:

1. in the culture, with a sign and a decimal point and no group separators;
2. the same, with the other of `.` and `,` taken as the culture's decimal point: a phone's decimal
   keypad shows the device's separator, which need not be the page's;
3. in the culture with its group separators, for a pasted `1 234,50`.

A reading with more fraction digits than the currency has is not a reading, so `1,234` in English is
one thousand two hundred and thirty-four (3.), never 1.234 (2.); en's `12,50` is 12.50 (2.), never 1250
(3.). An amount outside `min` and `max` is not committed, and the field says so; an empty field hands
back null. Each step is `decimal.TryParse`, so the browser reads as .NET does (#1087).

The amount crosses as `long` minor units, a `BigInt` in the browser: multiplying the decimal by the
currency's power of ten and converting is exact, and no `long` is divided.

## Risks / Trade-offs

- iOS's number and decimal keypads have no minus sign: a negative amount is typed on a hardware
  keyboard. A signed kind is a follow-up if an app needs one.
- The currency's symbol is fixed per `Currency` (US dollars are `$` in en-US and `US$` in pt-PT): an app
  that wants the other writes its own `Currency`.
