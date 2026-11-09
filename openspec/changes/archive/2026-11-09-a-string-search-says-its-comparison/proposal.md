## Why

Closes #532, #533, #965 and #729, all sub-issues of Feature #849.

Four places where text answered one way on the server and another in the browser:

- `StartsWith`, `EndsWith`, `IndexOf` and `LastIndexOf` of a string with no `StringComparison` search by the current culture in .NET, through ICU, and were JavaScript's ordinal searches: `"­ab".StartsWith("ab")` answered true on the server and false in the browser. Edgar's decision on #532: refuse them with EQ1004, the rule #528 applies to a culture comparison written out and what .NET's own CA1310 asks of a library.
- A culture comparison that ignores case compared with `Intl.Collator` at `sensitivity: 'accent'`, which equates half and full widths and kana types that .NET's collator is told to keep apart.
- A number or a date held as an interface or a type parameter was JavaScript's own text, which is invariant, where one held as `object` was the culture's.
- The twin's `px` rounded the double a float widens to, where the server's `TokenCss.Px` writes `ToString("0.##")`, so `1.005f` hydrated as another atomic class.

## What Changes

- A breaking change: a search with no comparison in code that reaches the browser fails the build (EQ1004), its method group too, and the message says what to write. A char's search and `Contains` are ordinal in .NET and stay. The Upgrading page says so.
- `utils/string-statics.ts` spells each form as the character it comes after, followed by a code point that sorts after every assigned one, so it is a primary difference right after its other form, as .NET's rules place it. Which forms is measured over the whole BMP, and a small katakana comes after the forms of its large one: ア, ｱ, ァ, ｧ. The tables are not copied from `pal_collation.c`: they follow from `NFKC`, a shift of 96 and a measured list of what stays apart.
- `StringConversion.HoldsAnyValue` sends an interface and a type parameter through the formatter, as an `object` goes, in the culture in force and in the invariant one.
- `px` writes a float's seven significant digits, rounded half away from zero to two decimals, the sign kept on a zero. The issue took the rule for the shortest text: measured, `123456.79f` writes `123456.8` and `16777216f` writes `16777220`.

## Impact

- Reaches `eQuantic.UI.Compiler` (`StringMethodStrategy`, `StringConversion`) and the runtime (`utils/string-statics.ts`, `shared/lowering.ts`).
- The migration line: pass `StringComparison.Ordinal` or `OrdinalIgnoreCase` to a string's `StartsWith`, `EndsWith`, `IndexOf` and `LastIndexOf`.
- Neither `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` nor `developer-surface.baseline.txt` moves.
