# amount-inputs Specification

## Purpose
How a typed value that is no prose is taken in a field of its own: a text input's prefix and suffix, an amount of
money in a currency's minor units, and a whole number with its unit, written and read in the page's culture.

## Requirements

### Requirement: A text input draws a prefix and a suffix beside its text

A `TextInput` and a `FormInput` SHALL take a `prefix` and a `suffix`, drawn before and after the entry
in the muted colour and the entry's type role, inside the field's box, and SHALL name the entry with the
label followed by the affixes in parentheses. A field with neither SHALL be named by its label alone.

#### Scenario: A unit

- **WHEN** a `TextInput` labelled `Weight` with the suffix `g` is drawn
- **THEN** `g` is drawn after the entry, and the entry's accessible name is `Weight (g)`

### Requirement: A money input writes an amount as the culture writes it

A `MoneyInput` SHALL take an amount in minor units and a `Currency`, and SHALL show it as the culture
writes a number with the currency's minor digits, grouped (`N`). What is typed SHALL stay as typed
until the field is left, and taking the focus SHALL rewrite nothing, so the caret stays where it was put. The currency's symbol SHALL be the field's prefix where the culture's
`CurrencyPositivePattern` puts the symbol first (0 and 2) and its suffix where it puts it last (1 and 3).
The entry SHALL be of kind `Decimal`.

#### Scenario: Euros in Portugal and in English

- **WHEN** a `MoneyInput` holding 123450 cents of `Currency.Euro` rests, in pt-PT and in en
- **THEN** pt-PT shows `1 234,50` with the suffix `€`, and en shows `1,234.50` with the prefix `€`

#### Scenario: Taking the focus

- **WHEN** the same field takes the keyboard in pt-PT, `1234,5` is typed, and the field is left
- **THEN** it shows `1 234,50` on focus, `1234,5` while typed, and `1 234,50` again

#### Scenario: A currency with no minor unit

- **WHEN** a `MoneyInput` holds 1235 of `Currency.Yen` in en
- **THEN** it shows `1,235`

### Requirement: A money input reads an amount as the culture reads it

A `MoneyInput` SHALL read what is typed in the culture with a sign and a decimal point, then with the
other of `.` and `,` as the decimal point, then with the culture's group separators, and SHALL take
the first reading with no more fraction digits than the currency has. It SHALL hand `OnChanged` the
amount in minor units as soon as what is typed is one inside `Min` and `Max`, and null when the field
is emptied. Text that is no amount, or an amount outside the range, SHALL NOT be handed on, and the
field SHALL say so with an error and mark the entry invalid. An error the app gives SHALL be shown
instead.

#### Scenario: The device's separator

- **WHEN** `12.5` is typed into a euro field in pt-PT, and `12,5` into one in en
- **THEN** each hands on 1250

#### Scenario: A thousand in English

- **WHEN** `1,234` is typed into a euro field in en
- **THEN** it hands on 123400

#### Scenario: Too many decimals

- **WHEN** `1,234` is typed into a euro field in pt-PT
- **THEN** nothing is handed on, and the field is invalid

#### Scenario: Out of range

- **WHEN** `5` is typed into a euro field whose `Min` is 1000
- **THEN** nothing is handed on, and the field is invalid

#### Scenario: Emptied

- **WHEN** a field holding an amount is emptied
- **THEN** it hands on null, and is not invalid

### Requirement: A number input takes a whole number with its unit

A `NumberInput` SHALL take a whole number and a unit, show the number as the culture writes it with
the unit as its suffix, grouped, read what is typed in the culture with a
sign and no group separator, open the digits keypad (kind `Number`), hand
`OnChanged` a number inside `Min` and `Max` and null for an emptied field, and say when what is typed is
no whole number or is outside the range.

#### Scenario: Days

- **WHEN** a `NumberInput` with the unit `dias`, `Min` 1 and `Max` 30 is typed `45`, then `3`
- **THEN** `45` is not handed on and the field is invalid, then 3 is handed on

#### Scenario: No group separator

- **WHEN** `2,5` is typed into a `NumberInput` in en
- **THEN** nothing is handed on, and the field is invalid; in .NET's `AllowThousands` it was twenty-five
