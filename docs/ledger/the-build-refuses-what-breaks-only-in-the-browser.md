- **2026-11-21 · The build refuses what an app met only in the browser**: a real site listed its
  browser-only pitfalls and scanned its own sources for one of them. Measured on `main`, `string.Format`,
  overloads and a `long` quotient were already refused or right; an `Icon` of 22 dp, a resx accessor read
  into a static and a type named like the SDK's passed the build. EQ3106 now reads a constant against its
  parameter's `[AllowedValues]` (the icon's four sizes), EQ3107 warns of a static read from a catalog,
  both analyzers ship in the web and the native SDK, and EQ1005 refuses a module that names a type of
  the app's and one of the SDK's under one name. And a stateful section a page hands what its prefetch
  loads was drawn with the page's defaults, since the server kept the instance its first frame built:
  while the data rounds run, only what loads keeps its instance now (`BuildOwner.KeepsInstance`)
  ([#1776](https://github.com/eQuantic/equantic-ui/issues/1776),
  [#1777](https://github.com/eQuantic/equantic-ui/issues/1777)). Proposed and archived through OpenSpec
  (`openspec/specs/build-analyzers`, `transpiler-names`, `element-tree`).
