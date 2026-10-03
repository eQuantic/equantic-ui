# Tasks

## 1. The writer

- [x] 1.1 Spell every string from its value in `JsStringLiteral`, as a quoted literal or a template's text, escaping each code unit that does not show as itself, and verify the module holds `'x\uD83D'` with a pair raw beside it
- [x] 1.2 Check: `LiteralConformanceTests` reads lone halves, a pair between them, the halves in a template, a folded and an inlined constant and every invisible class on both sides; 31 of its 39 cases fail on main at d09f7bec, where the module could not be written

## 2. The strategies

- [x] 2.1 Write a char literal and `nameof` from their value, and quote an interpolation's format, a skipped parameter's default and a resource lookup through the writer
- [x] 2.2 Keep a raw interpolated string's doubled braces, and refuse a literal with no JavaScript spelling with EQ1004
- [x] 2.3 Check: the conformance cases for the char escapes, the quoted format, the skipped defaults, `nameof(@class)` and the raw braces pass on both sides and fail on main; `LiteralSpellingTests` asks for EQ1004 and its message on `"ab"u8` and pins each spelling (22 of its 35 cases, with the resource and C# 13 tests, fail on main), and the C# 13 `\e` test reads the ESC character ordinally, since a culture-aware search finds a control at position 0 of any text

## 3. The map

- [x] 3.1 Check: `StackFrameSourceMapTests` throws after strings holding U+2028 and U+2029, and both frames lead to their own lines, where main led them two lines down (#491)

## 4. The unreachable writer

- [x] 4.1 Remove `ConvertToTsValue`, the unused `GetDefaultForType`, and the `DefaultValue` text they read, declaring the four signatures as `*REMOVED*`, and verify the solution builds with no warning

## 5. What ships

- [x] 5.1 Regenerate the transpiled pins and verify the change is spelling only (a tab is `\t`, a char `'\0'` is `'\u0000'`, a resource lookup is single-quoted), the runtime's own 1974 tests passing on them
- [x] 5.2 Check: the dashboard sample builds with a page holding `"x\uD83D"`, a pair, `'\a'` and `$"{date:dd 'de' MMMM}"`, which failed on main with EQ0001 and with Bun's syntax error; the module it serves holds the escape, and the browser, navigating to the page on the client, renders `2|55357|2|True|03 de outubro` as the server does

## 6. Documentation

- [x] 6.1 One `docs/LEDGER.md` line citing #520 and #491
- [x] 6.2 The wiki's SupportedFeatures and Compiler pages, English and Portuguese, on a wiki branch named like this pull request's
- [x] 6.3 File what this found outside its scope: a constant's numeric representation outside a literal (#523), a string read as a sequence (#524), the source map's and the manifest's JSON escaping (#525), and the server shell's hand-quoted configuration, measured stopping the client of every page (#526)
- [x] 6.4 Check: `./scripts/check-openspec.sh` validates the change strictly
