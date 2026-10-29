## MODIFIED Requirements

### Requirement: A char literal is its value

A char literal SHALL cross as the character C# reads, whichever escape spells it, and `nameof` SHALL
answer the name C# answers, not the identifier's spelling.

#### Scenario: The escapes only C# has

- **WHEN** `var s = "\a\e"; return s[0] == '\a' && s[1] == '\e';` runs
- **THEN** it answers true, as .NET does, where `'\a'` and `'\e'` were `'a'` and `'e'` to JavaScript

#### Scenario: A hexadecimal escape of any length

- **WHEN** `return "A" + '\x041' + '\U00000041';` runs
- **THEN** it answers `AAA`, as .NET does, where `'\x041'` was two characters

#### Scenario: Every spelling of a code unit

- **WHEN** each of `'\a'`, `'\e'`, `'\x7'`, `'\x007'`, `'\x0041'`, `'\0'`, `' '` and `'A'` runs alone as
  `var s = "" + <literal>; return s.Length + ":" + (int)s[0];`
- **THEN** each answers a length of 1 and the code .NET reads, `7`, `27`, `7`, `7`, `65`, `0`, `32` and `65`,
  where JavaScript read the token text of `'\x7'` as a syntax error, of `'\x007'` as two characters and of
  `'\x0041'` as three

#### Scenario: nameof of a verbatim identifier

- **WHEN** `var @class = 1; return nameof(@class);` runs
- **THEN** it answers `class`, as .NET does, where the twin answered `@class`
