## Why

#1874, a Bug under #1587, found surveying the directional components for #1569. `Pagination` wrote its "Previous" and "Next" steps and each page's accessible name ("Page 3") as English literals, where every other label of the SDK comes from `SdkStrings` and follows the UI culture. An app in Portuguese or Spanish showed English on its pager.

## What Changes

- `SdkStrings` gains `PreviousPage`, `NextPage` and `PageNumbered(int)`, read from `SdkResources` in every culture the SDK ships: the neutral English, Spanish, European Portuguese and Brazilian Portuguese.
- `Pagination` takes its steps and its pages' names from them, on the server and on Photon through the `ResourceManager`, and in the browser through eqc's accessor rewrite into the culture's catalog, the neutral strings being the last resort there as for every SDK string.

A developer writes nothing. `SdkStrings.PreviousPage`, `NextPage` and `PageNumbered` are new public surface of `eQuantic.UI.Components`, declared in its API file. Nothing goes.

## Capabilities

### New Capabilities

### Modified Capabilities

- `sdk-strings`: a pager's steps and pages are named in the reader's language.

## Impact

- `src/eQuantic.UI.Components/Pagination.cs`, `SdkStrings.cs`, `SdkResources*.resx` and its designer.
- The transpiled `Pagination` and `SdkStrings`, and `sdk-strings.generated.ts`.
- `tests/eQuantic.UI.Web.Tests/PaginationTests.cs` (new).
