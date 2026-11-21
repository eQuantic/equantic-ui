- **2026-11-21 · The SDK writes what an app kept by hand**: a real site kept three things the SDK
  already knew. Its catalogs' accessors: a neutral `.resx` with no Designer now gets its class from
  MSBuild's own `GenerateResource`, beside a mirror of the `.resx` where eqc reads a catalog, and the
  templates and samples drop their Designers with the same bytes in the browser. Its version, in two
  places: the SDK compares its own version with `EQuanticUIVersion`, and a different one fails with
  EQ4010 before the packages are restored. And `new` for a library's components: the components of a
  referenced project get factories like the app's own, and a package's get none
  ([#1784](https://github.com/eQuantic/equantic-ui/issues/1784),
  [#1788](https://github.com/eQuantic/equantic-ui/issues/1788),
  [#818](https://github.com/eQuantic/equantic-ui/issues/818)). Proposed and archived through OpenSpec
  (`openspec/specs/resource-accessors`, `sdk-version`, `component-factories`).
