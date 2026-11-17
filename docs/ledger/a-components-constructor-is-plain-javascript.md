- **2026-11-16 · A component's constructor is plain JavaScript**: the twin of a component that takes its parameters
  through its constructor writes them with no annotation when annotations are off, where it wrote `tag?: any` and
  `size: any = 2` into the plain module and bun refused it
  ([#1574](https://github.com/eQuantic/equantic-ui/issues/1574)). Proposed and archived through OpenSpec
  (`openspec/specs/transpiler-expressions`).
