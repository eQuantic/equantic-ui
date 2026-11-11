- **2026-11-10 · A date reads and builds as .NET does**: bugs under #849
  ([#807](https://github.com/eQuantic/equantic-ui/issues/807),
  [#952](https://github.com/eQuantic/equantic-ui/issues/952)). The reader took the numeric forms a culture writes;
  it reads a month's and a day's name now, in the culture's language and in English, two numbers, a clock alone as
  today's, `GMT`, and numbers white space sets apart, in the order the culture's patterns give, so `Jan 5` is 2005 in
  de-DE as in .NET; a word is read only where a word ends, as .NET's lexer reads it, and a time pattern's quoted word
  after the clock, bg-BG's `ч.` in Windows' data. `new TimeOnly` keeps its microsecond and checks every part. Proved by the conformance suite on
  both sides, 187 of 232 cases failing on main. A date in another calendar is read as a Gregorian one, which moved to
  #1191. Proposed and archived through OpenSpec (`runtime-dates`).
