# Tasks

## 1. The page

- [x] 1.1 Write the "The document is the theme's" theme with `FontFamily? TextFamily` in the five copies of `DesignSystem`, on the wiki branch named like this pull request's. Verify no copy of any page says `string? TextFamily` in a C# block.

## 2. The guard

- [x] 2.1 Write `WikiImplementationsTests`: every C# block compiled against the SDK's assemblies, each member of a type over an SDK interface compared with the interface's member of that name. Verify it fails on the wiki's master naming the five copies and their lines, and passes on this pull request's wiki branch.
- [x] 2.2 Reference `eQuantic.UI.Server` from `eQuantic.UI.Web.Tests`, and make an SDK interface the guard cannot resolve an offence. Verify the members compared go from 120 in 30 pages to 160 in 40.
- [x] 2.3 Controls without a wiki, one per kind of drift and one per shape that must pass. Verify `dotnet test tests/eQuantic.UI.Web.Tests` passes whole.

## 3. The record

- [x] 3.1 Write the ledger entry under `docs/ledger/`, citing #1226.
