# Spec Delta

## ADDED Requirements

### Requirement: A dictionary's keys and values are live views

`Keys` and `Values` SHALL read the dictionary when they are read, as .NET's collections of a
dictionary do, and a key added while they are walked SHALL end the walk with .NET's
InvalidOperationException.

#### Scenario: A view read after a change

- **WHEN** `var ks = d.Keys; d["z"] = 2;` runs on a dictionary holding `a`, and `ks` is read
- **THEN** it holds `a` and `z`, as in .NET

#### Scenario: A key added during the walk

- **WHEN** `foreach (var k in d.Keys) d["z"] = 2;` runs
- **THEN** it throws "Collection was modified; enumeration operation may not execute.", as .NET does

### Requirement: A dictionary keeps .NET's capacity

`EnsureCapacity` SHALL answer the prime .NET settles on, a capacity constructor SHALL size the
dictionary as .NET's does, and `TrimExcess` SHALL pack the entries and empty the free list where the
prime it asks for is smaller than the capacity there is.

#### Scenario: EnsureCapacity

- **WHEN** `new Dictionary<string, int>().EnsureCapacity(10)` is computed
- **THEN** it is 11, as in .NET

#### Scenario: TrimExcess

- **WHEN** five keys are added, two removed, `TrimExcess()` called and a key added
- **THEN** the keys enumerate with the new one last, as in .NET, where it took a freed slot
