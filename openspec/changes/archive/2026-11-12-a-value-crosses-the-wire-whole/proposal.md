## Why

Closes #590, #706 and #745, sub-issues of Feature #849.

- A hydrated record had none of its private state, so it equalled no value built in the browser, and a counter of its answered NaN. A struct's public field never arrived.
- A dictionary keyed by a record crossed with its keys as plain objects, which equal no key the browser builds.
- A twin's JSON wrote every own key, so fields and private state reached a server action, where `System.Text.Json` under `EqJson.Options` writes public properties.

## What Changes

- What crosses stays what `EqJson` writes, so nothing private reaches the page. The twin restores the rest: a record that declares a field is rebuilt through its primary constructor (`static $primary`), since an initializer may read the parameters, a struct from its zero, and hydration by the witness keeps what the value the page built holds.
- A dictionary's spec takes `keyOf` for a key that is no scalar, a record's or a struct's twin, and a key hydrates as a value does.
- `JsonContract` lists what the serializer writes for a type, from its static type: public instance properties with a public getter, what `[JsonInclude]` adds and `[JsonIgnore]` takes out, under the name `[JsonPropertyName]` gives. A twin that holds anything else hands the list to `$eq.json`, which writes each through its member.

## Limits

A private field the server changed does not cross: the browser's value holds what its initializer says. Writing private state into the payload would hand it to the page, which is why the wire stays as it is.

## Impact

- Reaches the compiler (`RecordTypeEmitter`, `TypeScriptEmitter`, `HydrationSpec`, a new `JsonContract`) and the runtime (`utils/hydrate.ts`, `utils/hydrate-value.ts`, `utils/twin-json.ts`).
- The conformance harness gains `ModuleGraph.AssertJsonSameAsDotNet`, which compares a value's JSON with `EqJson.Options`' in .NET.
- Neither `developer-surface.baseline.txt` nor the contract assemblies' API moves.
