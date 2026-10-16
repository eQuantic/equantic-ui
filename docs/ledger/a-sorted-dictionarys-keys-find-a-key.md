- **2026-10-14 · A sorted dictionary's keys find a key**: `SortedDictionary<TKey, TValue>.KeyCollection`
  declares its own `Contains`, and only a `Dictionary`'s keys' was mapped, so `d.Keys.Contains(k)` on a
  sorted dictionary failed the build with EQ2004 although the runtime's live view answers it. #919 added
  the case and #914 made a refused build fail its case; each pull request's CI ran without the other, so
  `main` failed that case once both merged. The call now lowers to the view's `contains`; two cases
  executed on both sides, both refused by the build before
  ([#928](https://github.com/eQuantic/equantic-ui/issues/928)). Proposed and archived through OpenSpec
  (`openspec/specs/runtime-dictionaries`).
