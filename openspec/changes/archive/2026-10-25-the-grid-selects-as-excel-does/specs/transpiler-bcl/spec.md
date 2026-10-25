## ADDED Requirements

### Requirement: A number is read in the culture the call names

`Parse` and `TryParse` of a `decimal`, a `double`, a `float` and the eight integer types, and
`Convert.ToDecimal`, `Convert.ToDouble`, `Convert.ToSingle` and the integer conversions of a text or of
a value that may hold one, SHALL read the text as .NET 10 reads it in the culture the call names: with
no provider, with `CultureInfo.CurrentCulture` and with a null one, a null behind a cast included, the
page's culture, which is the current one; with `CultureInfo.InvariantCulture`, the invariant one. The
culture SHALL give the signs, the decimal separator and the thousands separator, and, where the style
allows a currency symbol, the currency's symbol and its own separators, the number's being read too
until the symbol is met; a no-break space in a culture's symbol SHALL also be read as a space, a culture
whose minus is one of the dashes CLDR names SHALL also read a hyphen as its minus, and a real's words
for infinity and for what is not a number SHALL be the culture's. Any other provider SHALL fail the
build (EQ2108). The build SHALL no longer warn about a number read with no culture.

#### Scenario: A decimal comma on a Portuguese page

- **WHEN** `decimal.TryParse("7,01", NumberStyles.Float, CultureInfo.CurrentCulture, out var d)` runs in
  pt-BR
- **THEN** it answers true and 7.01, as in .NET, where the build refused the current culture (EQ2108)

#### Scenario: No provider reads the page's culture

- **WHEN** `double.Parse("1,5")` runs in de-DE
- **THEN** it answers 1.5, as in .NET, where the browser read the invariant culture and answered 15

#### Scenario: A thousands separator typed as a space

- **WHEN** `decimal.Parse("1 000,5")` runs in fr-FR, whose separator is U+202F
- **THEN** it answers 1000.5, as in .NET

#### Scenario: A hyphen where the culture's minus is U+2212

- **WHEN** `int.Parse("-5")` and `int.Parse("−5")` run in sv-SE
- **THEN** both answer -5, as in .NET

#### Scenario: The invariant culture on any page

- **WHEN** `decimal.Parse("7.01", CultureInfo.InvariantCulture)` runs in pt-BR
- **THEN** it answers 7.01, as in .NET

#### Scenario: A null provider behind a cast

- **WHEN** `double.Parse("2.5", (IFormatProvider?)null)` is built
- **THEN** it builds and reads the page's culture, as a bare null does, where the build refused it
  (EQ2108)

#### Scenario: Another culture

- **WHEN** `decimal.Parse(text, CultureInfo.GetCultureInfo("pt-BR"))` is built
- **THEN** the build fails with EQ2108
