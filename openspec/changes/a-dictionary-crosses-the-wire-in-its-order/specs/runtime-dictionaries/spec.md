# Spec Delta

## MODIFIED Requirements

### Requirement: A dictionary crosses the wire as its class

A dictionary held in server-rendered state, answered by a Server Action or published on a server
topic SHALL arrive as the dictionary class, its keys in their type, enumerating in the order the
server enumerated it, and a dictionary sent to the server as a Server Action argument SHALL arrive
enumerating in the order the browser enumerated it. Both directions SHALL write it as a JSON array
of `[key, value]` pairs, each key and each value written as a value of its type is written.

#### Scenario: A state field with integer keys

- **WHEN** a `Dictionary<int, string>` field holding the keys 3, then 1 arrives in server-rendered state
- **THEN** it hydrates into the dictionary class, `ContainsKey(3)` answers true, its keys are numbers, and they enumerate "3,1", as in .NET

#### Scenario: Keys that look like integers

- **WHEN** a Server Action answers a `Dictionary<string, int>` holding the keys "b", then "3"
- **THEN** the browser's dictionary enumerates "b,3", as in .NET

#### Scenario: A dictionary argument

- **WHEN** a dictionary holding the keys 3, then 1 is sent as a Server Action argument
- **THEN** it serializes as `[[3,…],[1,…]]`, and the server's dictionary enumerates "3,1"

#### Scenario: A key of a type JSON has no number for

- **WHEN** a `Dictionary<long, string>` keyed by 9007199254740993 crosses in either direction
- **THEN** the key is written as its text, "9007199254740993", and arrives as that long

#### Scenario: A dictionary-like value of another type

- **WHEN** a member declared `IReadOnlyDictionary<int, string>` holds an `ImmutableDictionary`, which System.Text.Json writes as a JSON object
- **THEN** it still hydrates into the dictionary class, in the order the parsed object holds
