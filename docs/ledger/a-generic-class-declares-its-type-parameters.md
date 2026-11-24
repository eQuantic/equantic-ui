- **2026-11-23 · A plain generic class, a record and a struct declare their type parameters**: only the component
  path wrote a class's type parameters, so `GestureRecognizerFactoryWithHandlers<T>` named `T` in its members and
  declared it nowhere, and the runtime's `tsc` refused it in the library (#1563). A generic record and struct had the
  same gap, and a record's members named its own type without its closure. One list, `TwinTypeParameters`, is now read
  by every kind of class, and a record names its own type by its closure. The class writer writes the list only in
  TypeScript, so a generic component compiled to plain JavaScript, which was `class Listing<TItem> {`, parses
  ([#1845](https://github.com/eQuantic/equantic-ui/issues/1845)). Proposed and archived through OpenSpec
  (`openspec/specs/transpiler-classes`).
