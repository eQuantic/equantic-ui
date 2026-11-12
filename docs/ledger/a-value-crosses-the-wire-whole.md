- **2026-11-12 · A value crosses the wire whole**: bugs under #849 ([#590](https://github.com/eQuantic/equantic-ui/issues/590),
  [#706](https://github.com/eQuantic/equantic-ui/issues/706), [#745](https://github.com/eQuantic/equantic-ui/issues/745)). A hydrated
  record had no private state and equalled nothing built here: a record that declares a field is rebuilt through its primary constructor and
  a struct from its zero, and the wire stays what `EqJson` writes, so a private field the server changed does not cross. A dictionary's
  key hydrates as a value, a record's on its twin. A twin that holds more than the serializer writes hands `$eq.json` the list its static
  type says, so fields no longer reach a server action.
