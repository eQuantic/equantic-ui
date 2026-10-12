# Spec Delta

## ADDED Requirements

### Requirement: A character takes the cells Unicode gives it

The code engine SHALL count a character that begins a text element by the Unicode version the SDK's
embedded bun follows, the same in .NET and in the browser: two cells for an East Asian Wide or
Fullwidth character, which takes in an emoji drawn as one by default, none for a format character
that is not drawn, and one for any other. A Hangul vowel or final SHALL take no cell of its own after
its consonant, whose element it is part of, and one cell when it begins an element.

#### Scenario: Characters Unicode 16 and 17 made or added wide

- **WHEN** a line holds a Yijing hexagram (U+4DC0), a Tai Xuan Jing symbol (U+1D300) and an emoji
  Unicode 16 added (U+1FADC)
- **THEN** each takes two cells, in .NET and in the browser

#### Scenario: A directional isolate, and a Hangul syllable written in conjoining jamo

- **WHEN** a line holds a left-to-right isolate (U+2066), and the syllable written as U+1100, U+1161
  and U+11A8
- **THEN** the isolate takes no cell, and the syllable takes two

#### Scenario: A Hangul vowel with no consonant before it

- **WHEN** a line holds the letter `a` and then the vowel U+1161
- **THEN** the vowel takes one cell, in .NET and in the browser

#### Scenario: Every character, against the embedded bun

- **WHEN** every assigned character that can begin a text element is measured in .NET, in the
  runtime's twin and by `Bun.stringWidth` of the embedded bun
- **THEN** the three agree, but for the three Indic letters the oracle counts as no cell, and for the
  Hangul vowels and finals, which take one cell on both sides where the oracle counts none
