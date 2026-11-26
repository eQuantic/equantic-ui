- **2026-11-24 · A pager speaks the reader's language**: a Bug under #1587
  ([#1874](https://github.com/eQuantic/equantic-ui/issues/1874)). `Pagination` wrote "Previous", "Next" and each
  page's accessible name ("Page 3") in English in every culture. They come from `SdkStrings` now
  (`PreviousPage`, `NextPage`, `PageNumbered`), in every culture the SDK ships, as every other label of the SDK
  does. `PaginationTests` reads them in four cultures. Proposed and archived through OpenSpec
  (`openspec/specs/sdk-strings`).
