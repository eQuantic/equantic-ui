## 1. An initializer evaluates a call's arguments as written

- [x] 1.1 `ConvertInitializer` converts an initializer in a scope of its own and declares what it binds in its arrow. Verify: the initializer cases of `WrittenArgumentOrderConformanceTests` (a field and a property, a record's property, a static) fail on the branch before the change (`2143` against `1234`) and pass after, in TypeScript and in JavaScript
- [x] 1.2 Every initializer goes through it, and `ExpressionVariableScanner.Scoped` goes. Verify: the build declares the change of the tool surface

## 2. Proved against the real thing

- [x] 2.1 The runtime's transpiled libraries, regenerated and read: an object initializer in a field's initializer binds its temporary in the arrow. Verify: `SharedComponentTranspilationTests` pass on the regenerated modules, and the runtime's `tsc` and vitest pass (204 files, 2975 tests)
- [x] 2.2 The lowerings that now bind in an initializer, executed on both sides. Verify: the conformance tests of object initializers, null-conditionals, initializers and argument order pass, 153 of 153
- [x] 2.3 Docs. Verify: the wiki's SupportedFeatures line says an initializer evaluates its arguments as written, in five languages, and a ledger entry cites #1201
