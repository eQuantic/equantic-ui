# transpiler-expressions Specification

## Purpose
How eqc translates a C# expression, pattern or local into the JavaScript twin: the answer the twin
gives is the one .NET gives, and plain JavaScript holds nothing of TypeScript.

## Requirements

### Requirement: An extended property pattern reads each member of its path

An extended property pattern (`{ A.B: pattern }`) SHALL be translated as the nested pattern
`{ A: { B: pattern } }`: each member of the path named as a member access names it, and a member
that is null before the last SHALL make the pattern answer false.

#### Scenario: A count through a member

- **WHEN** `new Bag(new List<int> { 1, 2 }, null) switch { { Items.Count: > 1 } => 1, _ => 0 }` runs
- **THEN** it answers 1, as .NET does

#### Scenario: A null on the path

- **WHEN** `new Bag(new List<int> { 1 }, null) switch { { Corner.X: 0 } => 1, _ => 0 }` runs
- **THEN** it answers 0, as .NET does, and nothing throws

### Requirement: A string crosses whole

Every string the transpiler writes from a C# value SHALL be the value .NET holds, code unit for code
unit, in a module that parses and can be written as UTF-8: a string literal, a char literal, an
interpolated string's text, a `const` inlined where it is read, an interpolation's format, the
default a named argument skips, a resource lookup's id and key, and `nameof`. A line break, a
surrogate that is not half of a pair, a control, a format character, U+2028 and U+2029 SHALL be
written as escapes, and a well-formed surrogate pair SHALL stay one code point.

#### Scenario: A default holding a line break

- **WHEN** `record Sep(string Joined = "a" + "\n", char Line = '\n', string Plain = "\r\n")` is
  constructed without arguments and its three members are concatenated
- **THEN** the answer is `a`, a line feed, a line feed, a carriage return and a line feed, as .NET
  gives it

#### Scenario: A lone surrogate in a string literal

- **WHEN** `return (int)char.GetUnicodeCategory("x\uD83D", 1);` runs
- **THEN** it answers 16 (Surrogate), as .NET does, where the module holding the literal could not
  be written and the build stopped with EQ0001

#### Scenario: A pair between two lone halves

- **WHEN** `var s = "\uD83D\uD83D\uDE00\uDE00";` is read with `char.IsSurrogatePair(s, 1)`,
  `char.IsSurrogatePair(s, 0)` and `char.ConvertToUtf32(s[1], s[2])`
- **THEN** the answers are true, false and 128512, as .NET gives them

#### Scenario: A format with quoted text

- **WHEN** `$"{new DateTime(2026, 10, 3):dd 'de' MMMM}"` runs under the invariant culture
- **THEN** it answers `03 de October`, as .NET does, where Bun refused the module

#### Scenario: A skipped default holding a quote

- **WHEN** `string Join(string a = "it's", char c = ',', int b = 0) => a + c + b;` is called as
  `Join(b: 1)`
- **THEN** it answers `it's,1`, as .NET does, where the call closed the default's quotes

### Requirement: Plain JavaScript carries no annotation

A module eqc writes without type annotations, as the design host asks, SHALL hold no TypeScript
syntax, a local's declared type included.

#### Scenario: A local that starts null

- **WHEN** `string? label = null;` is compiled with type annotations off
- **THEN** it is written `let label = null;`, and with them on `let label: string | null = null;`

### Requirement: A char literal is its value

A char literal SHALL cross as the character C# reads, whichever escape spells it, and `nameof` SHALL
answer the name C# answers, not the identifier's spelling.

#### Scenario: The escapes only C# has

- **WHEN** `var s = "\a\e"; return s[0] == '\a' && s[1] == '\e';` runs
- **THEN** it answers true, as .NET does, where `'\a'` and `'\e'` were `'a'` and `'e'` to JavaScript

#### Scenario: A hexadecimal escape of any length

- **WHEN** `return "A" + '\x041' + '\U00000041';` runs
- **THEN** it answers `AAA`, as .NET does, where `'\x041'` was two characters

#### Scenario: nameof of a verbatim identifier

- **WHEN** `var @class = 1; return nameof(@class);` runs
- **THEN** it answers `class`, as .NET does, where the twin answered `@class`

### Requirement: A raw interpolated string keeps its braces

A raw interpolated string's braces SHALL be its text unless as many of them as the string has
dollars open a hole; only a regular or a verbatim interpolated string SHALL read a doubled brace as
one brace.

#### Scenario: Three dollars and two braces

- **WHEN** `var n = 1; return $$$"""a{{b}}c{{{n}}}""";` runs
- **THEN** it answers `a{{b}}c1`, as .NET does, where the twin answered `a{b}c1`

#### Scenario: A regular interpolated string's doubled brace

- **WHEN** `var n = 1; return $"{{a}}{n}";` runs
- **THEN** it answers `{a}1`, as .NET does

### Requirement: A literal with no JavaScript spelling is refused

A literal that JavaScript has no spelling for SHALL fail the build with EQ1004 at the literal,
and SHALL NOT be written into the module as C#.

#### Scenario: A UTF-8 literal

- **WHEN** a component reads `"ab"u8.Length`
- **THEN** the build fails with EQ1004 naming the literal, where Bun met `"ab"u8.length` in the
  module and reported a syntax error against no C# line

### Requirement: A null-conditional call evaluates as C# does, an awaited argument included

A null-conditional access (`a?.M(x)`, `a?[i]`, and a chain behind one) SHALL evaluate its receiver
once and SHALL answer null when the receiver is null, without evaluating the rest of the chain or
its arguments. An argument that awaits SHALL be awaited in the method it is written in, whatever
the call translates to, and the module SHALL parse.

#### Scenario: An awaited argument behind a string

- **WHEN** `string s = "abc";` runs `var r = s?.StartsWith(await Needle(), StringComparison.Ordinal);`,
  where `Needle` counts its calls and answers `"a"`
- **THEN** `r` is true and `Needle` was called once

#### Scenario: An awaited argument behind a null receiver

- **WHEN** `string s = null;` runs the same line
- **THEN** `r` is null and `Needle` was never called
