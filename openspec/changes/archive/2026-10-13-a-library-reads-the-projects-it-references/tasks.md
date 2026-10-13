# Tasks

## 1. The order

- [x] 1.1 Reproduce Cura's EQ2006 on the published 0.2.0-preview.62 with a solution of an app, a library and the project it reads, and measure which assemblies the library's own names
- [x] 1.2 `ProjectGraph` reads which project references which from the app's assets file, and `ProjectCompilations` orders the projects by it
- [x] 1.3 The SDK hands eqc `--project-assets $(ProjectAssetsFile)` with the projects, and a missing graph or a project it does not hold stops the build (EQ0001)

## 2. The proof

- [x] 2.1 The test solution hands each project's reference assembly and an assets file, as the SDK does
- [x] 2.2 A library that reads a constant of a project and uses one of its types only inside a method builds, the same solution with the edge left out of the graph fails EQ2006, and no graph stops the build
- [x] 2.3 The shared-library sample reads `SharedLibrary.Theme` through a constant, its CI check reads the value in the page's bundle, and on main's compiler it fails EQ2006
- [x] 2.4 The sample's page shows the value in a browser

## 3. The record

- [x] 3.1 The wiki's Build Flow page says a library reads what it references, in English and Portuguese
- [x] 3.2 One `docs/LEDGER.md` line citing the issue
- [x] 3.3 Archive the change with `openspec archive a-library-reads-the-projects-it-references --yes`
