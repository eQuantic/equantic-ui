# Spec Delta

## ADDED Requirements

### Requirement: A character takes the cells Unicode gives it

The code engine SHALL give a text element the cells its first character takes by the Unicode version
the SDK's embedded bun follows, the same in .NET and in the browser: two for an East Asian Wide or
Fullwidth character and for an emoji drawn as one, none for a format character and for a Hangul vowel
or final, which join what is before them, and one for any other.

#### Scenario: Characters Unicode 16 and 17 made or added wide

- **WHEN** a line holds a Yijing hexagram (U+4DC0), a Tai Xuan Jing symbol (U+1D300) and an emoji
  Unicode 16 added (U+1FADC)
- **THEN** each takes two cells, in .NET and in the browser

#### Scenario: A directional isolate, and a Hangul syllable written in conjoining jamo

- **WHEN** a line holds a left-to-right isolate (U+2066), and the syllable written as U+1100, U+1161
  and U+11A8
- **THEN** the isolate takes no cell, and the syllable takes two

#### Scenario: Every character, against the embedded bun

- **WHEN** every assigned character that can begin a text element is measured in .NET, in the
  runtime's twin and by `Bun.stringWidth` of the embedded bun
- **THEN** the three agree, but for the three Indic letters the oracle counts as no cell
