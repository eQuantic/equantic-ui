# Tasks

## 1. The runtime's searches

- [x] 1.1 `utils/string-search.ts`: `StartsWith`, `EndsWith`, `IndexOf` and `LastIndexOf` (a start and a count, in C#'s argument order), `Contains`, `Replace`, `Equals` and `CompareTo`, porting .NET 10's `Ordinal`, `OrdinalCasing` and `CompareInfo`'s checks and words; check: `string-search.spec.ts`, its answers measured on .NET 10, through `dotnet build src/eQuantic.UI.Runtime -t:TestRuntime`
- [x] 1.2 A search by a culture comparison throws after .NET's checks, naming the method, the comparison and the fix; check: the spec's culture cases

## 2. The compiler chooses by the bound method

- [x] 2.1 `StringMethodStrategy` reads the overload from the bound method's parameters, and `IsNamed` matches a `CultureInfo?` by its name without the annotation (`TypeIsNamedTests`); it calls `$eq.text` with the arguments as C# writes them, `Replace(string, string)` as the ordinal replace; with no model, by the count of the arguments and a comparison spelled last; check: `StringComparisonOverloadTests`
- [x] 2.2 A constant culture comparison in a search, and an overload taking a `CultureInfo`, are refused with EQ1004; `CompareTo(string)` is the current culture's and `CompareTo(object)` is refused; check: `StringComparisonOverloadTests`
- [x] 2.3 Check against the real thing: `InstanceStringComparisonConformanceTests` run on both sides, 46 cases, 27 of them failing on main, and the whole conformance suite green

## 3. What moved with it

- [x] 3.1 The write-once components' pins regenerated: their ordinal searches and `Replace` call the runtime, with no arrow function around a call with a range; check: `SharedComponentTranspilationTests`, and the dashboard sample in a browser (the payments filter by `OrdinalIgnoreCase`, the code editor's block comment, the Markdown, Mermaid and diff pages)
- [x] 3.2 The BCL audit baseline: fifteen overloads move from native to the runtime, and `CompareTo(object)` is fenced; check: `BclSurfaceAuditTests`
- [x] 3.3 The follow-ups filed under #164: #532, #533 and #534

## 4. Documentation

- [x] 4.1 The wiki's SupportedFeatures and Compiler pages in English and Portuguese, on the wiki branch `fix/a-string-method-compares-as-dotnet-does`, merged at the merge
- [x] 4.2 One `docs/LEDGER.md` line citing #528
- [x] 4.3 Archive this change in the same pull request
