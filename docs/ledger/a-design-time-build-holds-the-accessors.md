- **2026-11-21 · An app with no Designer keeps its hot reload**: since the SDK writes a catalog's accessors
  (#1784), every save under `dotnet watch` in an app whose `.resx` has no `Designer.cs` failed with CS0234
  on the catalog's namespace, so no edit reached the server and the page drew over its markup:
  `samples`' web "Edit to view" measured a warm median of about 5,050 ms against its 1,500 ms ceiling.
  MSBuild writes the class inside `PrepareResources`, which `Compile` alone does not run, and dotnet
  watch loads its compilation from a design-time build of `Compile`, as an editor does. Both SDKs now
  run `PrepareResources` before `CoreCompile`; a design-time build of a plain project carries the class
  in both, which it did in neither without the target, and the dashboard's warm median is 253 ms
  ([#1801](https://github.com/eQuantic/equantic-ui/issues/1801)). Proposed and archived through
  OpenSpec (`openspec/specs/resource-accessors`).
