## ADDED Requirements

### Requirement: A text changes case as .NET changes it

`ToUpper` and `ToLower` and their invariant pair, of a `string` and of a `char`, SHALL change each
UTF-16 code unit, or surrogate pair, to exactly one other, by .NET 10's simple case mappings, so a
text's length never changes: a letter whose full mapping lengthens SHALL keep its case, but for the
Greek letters with an iota subscript, which SHALL upper-case to the capital with a prosgegrammeni; a
capital sigma SHALL lower-case to the medial one wherever it stands; a lone surrogate SHALL stay as it
is; and a code point .NET 10 does not case over a Linux or Windows server's ICU SHALL keep its case
on an engine of a later Unicode, where .NET on macOS 26 cases it by Apple's ICU. The two
i's SHALL be the culture's: tr and az, by their language, SHALL upper-case `i` to `İ` and lower-case
`I` to `ı`, every other culture SHALL upper-case `ı` to `I` and lower-case `İ` to `i`, and the
invariant culture SHALL leave `İ` and `ı` as they are. No culture, `CultureInfo.CurrentCulture` and a
null SHALL read the page's culture, which is the current one, and `CultureInfo.InvariantCulture` and
the invariant pair the invariant one; any other culture SHALL fail the build (EQ2108). A char's culture
handed as null SHALL throw .NET's ArgumentNullException after the char is evaluated, and a char's
casing with a culture taken as a delegate SHALL fail the build (EQ1004).

#### Scenario: A letter whose full upper case lengthens

- **WHEN** `"straße".ToUpper()` runs
- **THEN** it answers `STRAßE`, as in .NET, where it answered `STRASSE`

#### Scenario: A final sigma

- **WHEN** `"ΟΔΟΣ".ToLower()` runs
- **THEN** it answers `οδοσ`, as in .NET, where it answered `οδος`

#### Scenario: The i of a Turkish page

- **WHEN** `"i".ToUpper()` runs in tr-TR
- **THEN** it answers `İ`, as in .NET, where it answered `I`

#### Scenario: The invariant culture keeps the dotless i

- **WHEN** `"ı".ToUpperInvariant()` runs
- **THEN** it answers `ı`, as in .NET, where it answered `I`

#### Scenario: A char's null culture

- **WHEN** `char.ToUpper('a', null)` runs
- **THEN** it throws ArgumentNullException for `culture`, as in .NET, where it answered `A`

#### Scenario: A culture the browser does not know

- **WHEN** `text.ToUpper(CultureInfo.GetCultureInfo("tr-TR"))` is built
- **THEN** the build fails with EQ2108, where the culture was dropped
