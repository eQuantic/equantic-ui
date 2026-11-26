- **2026-11-26 · The emission shapes are one file each, read from their folder**: the shapes eqc's twins once failed the
  runtime's `tsc` on were one inline source in `SharedComponentTranspilationTests` and one list of the modules' names
  that kept them out of the runtime, and every pull request adding a shape appended to both, so the merge queue met the
  conflict there three times. Each shape is now a file of its own under `tests/eQuantic.UI.Web.Tests/Fixtures/Shapes`,
  read from the folder, and a module is an in-test proof when it comes from no library source, so a new shape is its own
  new file. The committed modules and the runtime's `tsc` are unchanged byte for byte
  ([#1906](https://github.com/eQuantic/equantic-ui/issues/1906)).
