# Tasks

## 1. The server writes and reads the pairs

- [ ] 1.1 A dictionary converter in `EqJson`: pairs written in enumeration order, keys and values by
      their own converters, pairs or an object read back, for the six types and none other
- [ ] 1.2 The state payload written as objects by name, each field's value through `EqJson`
- [ ] 1.3 A projected service's members as `ProjectedMembers`, which the converter does not match
- [ ] 1.4 `EqJsonTests`: integer and integer-like string keys in slot order, each key type, both
      reads, the sorted two, and the state payload's objects
- [ ] 1.5 Check: `eQuantic.UI.Server.Tests`, with the payload fixture regenerated and read back

## 2. The browser writes and reads the pairs

- [ ] 2.1 `Dictionary` and `SortedMap` write their pairs with `toJSON`
- [ ] 2.2 Hydration builds the class from the pairs, reviving each key by its tag, and still from an
      object
- [ ] 2.3 `dictionary.spec.ts`, `sorted.spec.ts` and `hydrate.spec.ts` pin the order both ways
- [ ] 2.4 Check: the runtime's suite (`-t:TestRuntime`) and the Server suite (the served runtime's
      budget)

## 3. Both sides agree

- [ ] 3.1 `RuntimeJson` writes a dictionary as the runtime's `toJSON` does
- [ ] 3.2 `Json_MatchesDotNet` crosses integer and integer-like keys
- [ ] 3.3 Check: the conformance suite, executed on both sides, and a Server Action round trip in a
      browser on a sample

## 4. Documentation

- [ ] 4.1 The wiki page that describes what crosses the wire, in English and Portuguese
- [ ] 4.2 One `docs/LEDGER.md` line citing #437
