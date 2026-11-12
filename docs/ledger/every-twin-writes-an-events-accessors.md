- **2026-11-12 · Every twin writes an event's accessors, a record's, a struct's and a component's too**: a bug under #849
  ([#1445](https://github.com/eQuantic/equantic-ui/issues/1445)). Only a class's twin wrote the `$add$` and `$remove$`
  methods of an event declared with `add` and `remove`; a record, a struct or a component that declared one lost
  its accessors, so a subscription reached nothing. The writer moved to the lowering both emitters share
  (`MethodLowering.EventAccessors`, beside `Indexer`), and the class, record, struct and component emitters all
  call it; the class path's output did not change. `EventAccessorConformanceTests` runs one on each of the four
  and the browser answers what .NET does. Proposed and archived through OpenSpec (`transpiler-classes`).
