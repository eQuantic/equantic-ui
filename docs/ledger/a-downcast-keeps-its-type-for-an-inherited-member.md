- **2026-11-11 · A downcast to a runtime node over a base the server keeps keeps its type**: a bug under #1174 on
  the 1.0 critical path ([#1396](https://github.com/eQuantic/equantic-ui/issues/1396)). #1350 dropped a downcast's
  type for a type kept on the server, and counted a class whose base is `[ServerOnly]` as one, which is right for an
  app's class and wrong for a type of the runtime, which carries a twin of its own: `((Pressable)node).Child` was
  written `node.child`, which `tsc` refused 24 times in the element engine's `ChildSlots`. A type of the runtime now
  drops the cast only where it is itself `[ServerOnly]`. Proposed and archived through OpenSpec
  (`transpiler-classes`).
