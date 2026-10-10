# Tasks

## 1. The server writes and reads the pairs

- [x] 1.1 A dictionary converter in `EqJson`: pairs written in enumeration order, keys and values by
      their own converters, read back only as pairs, for every dictionary of .NET's own collections
- [x] 1.2 The state payload written as objects by name, each field's value through `EqJson`
- [x] 1.3 A projected service's members as `ProjectedMembers`, which the converter does not match
- [x] 1.4 `EqJsonTests`: integer and integer-like string keys in slot order, each key type, the
      read of the pairs and the refusal of an object, the sorted two, and the state payload's objects
- [x] 1.5 Check: `eQuantic.UI.Server.Tests`, with the payload fixture regenerated and read back

## 2. The browser writes and reads the pairs

- [x] 2.1 `Dictionary` and `SortedMap` write their pairs with `toJSON`
- [x] 2.2 Hydration builds the class from the pairs, reviving each key by its tag, and still from an
      object
- [x] 2.3 `dictionary.spec.ts`, `sorted.spec.ts` and `hydrate.spec.ts` pin the order both ways
- [x] 2.4 Check: the runtime's suite (`-t:TestRuntime`) and the Server suite (the served runtime's
      budget)

## 3. Both sides agree

- [x] 3.1 `RuntimeJson` writes a dictionary as the runtime's `toJSON` does
- [x] 3.2 `Json_MatchesDotNet` crosses integer and integer-like keys
- [x] 3.3 Check: the conformance suite, executed on both sides (the crossing renders on the server
      and builds the twin in Bun on that payload), and a Server Action's request pinned in one file
      that the runtime writes and the server's middleware reads

## 4. Documentation

- [x] 4.1 The wiki page that describes what crosses the wire, in English and Portuguese
- [x] 4.2 One `docs/LEDGER.md` line citing #437
