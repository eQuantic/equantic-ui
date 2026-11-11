## 1. One rule

- [x] 1.1 `ComponentRule`, read by the parser and the scan. Verify: `TheScanFilesAClass_AsTheParserWritesIt` fails on main for a private `Build`, a static `Render` and a `CreateState`, and the classification matrix passes

## 2. The plain class imports its helper

- [x] 2.1 The plain-class path reads the scan's runtime-provided types. Verify: `RuntimeProvidedStaticHelper_FallbackImportsRuntime_FromAPlainClass` fails on main

## 3. The member fails the build

- [x] 3.1 `ReportIfExceptionWithNoModule` at the fence every member passes. Verify: `AnExceptionInASnippetTests` fails on main for the three reads of #754, and what the runtime carries builds

## 4. Against the real thing

- [x] 4.1 The whole Compiler suite, and CI's Conformance and samples

## 5. Documentation

- [x] 5.1 The wiki's SupportedFeatures page in all five languages
- [x] 5.2 One entry for the ledger citing #754 and #1170, as a file of its own under `docs/ledger/`
