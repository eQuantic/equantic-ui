- **2026-10-30 · A twin says the type C# declared**: a local function carries its declared return type, where
  TypeScript inferred the derived type its body returned and a variable it initialized refused a sibling
  ([#793](https://github.com/eQuantic/equantic-ui/issues/793)), and an object initializer's temporary is declared as the
  type it holds, where `any` left a lambda subscribed to the local's event with nothing to infer from
  ([#1106](https://github.com/eQuantic/equantic-ui/issues/1106)). Only the TypeScript changes, 24 transpiled components
  of it, and the runtime's `tsc` reads them clean.
