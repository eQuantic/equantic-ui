- **2026-11-26 · A collection's field and local keep their types in TypeScript**: a field of a queue was declared by a
  `Queue<T>` its module never imported, and a local built with `new` lost its element type, so the runtime's `tsc`
  refused a twin holding either. A field of every runtime collection class is now declared `any`, as a dictionary's was,
  and the factories carry the type arguments of the type they build (`$eq.collections.queue<Shape>()`), for `new` and
  for a collection expression, in a module written with type annotations, a type kept on the server as `any`
  ([#1866](https://github.com/eQuantic/equantic-ui/issues/1866)). No module's JavaScript changed. Proposed and archived through OpenSpec (`openspec/specs/runtime-collections`).
