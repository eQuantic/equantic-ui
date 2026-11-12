- **2026-11-11 · An array of an enum is typed as the enum crosses**: bugs under #849
  ([#1064](https://github.com/eQuantic/equantic-ui/issues/1064), [#1065](https://github.com/eQuantic/equantic-ui/issues/1065)).
  A field typed as an array of an enum named the enum's C# name, `static get notACellsCommand(): CodeCommand[]`,
  which no module exports, so the runtime's `tsc` refused the library's twin: the swap to what an enum crosses as
  looked inside a generic's arguments and never inside an array's element. It now does, at any depth, so
  `TypeRole[]` is `TypeRoleValue[]` and an app enum's array `string[]`. A component's `ColorToken` field, which
  #1142 already declares definitely assigned, is pinned as the issue asked. Proposed and archived through OpenSpec
  (`transpiler-classes`).
