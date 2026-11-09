# Tasks

## 1. The catalog

- [x] 1.1 Write `SdkResources.pt.resx` with every key in European Portuguese. Verify a component's text reads it in `pt`, `pt-PT`, `pt-AO` and `pt-MZ`, and `pt-BR` keeps its own.
- [x] 1.2 Fail on a catalog that lacks a key, carries one the neutral catalog does not, or differs in a placeholder. Verify the test fails with the `pt` catalog taken away and passes with it.

## 2. The browser

- [x] 2.1 Verify the build writes `strings/pt.json` (`dotnet build samples/DefaultUIDashboard`: "neutral + es, pt, pt-BR"), and pin that a `pt-PT` page reads it through the server's walk while `pt-BR` reads its own.

## 3. The docs

- [x] 3.1 Name the `pt` catalog on the wiki's Localization page in all five languages, and write the ledger entry citing #1221.
