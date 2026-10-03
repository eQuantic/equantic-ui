# Spec Delta

## ADDED Requirements

### Requirement: A string moves no line of the map

A string the transpiler writes SHALL hold no character JavaScript counts as a line terminator, so
every mapping after it keeps its line.

#### Scenario: A frame after strings holding U+2028 and U+2029

- **WHEN** a method builds `"a\u2028b"` and `$"{plain}\u2029{count}\u2028"`, then calls a method that
  throws, in the module a build bundles with the embedded Bun
- **THEN** the throw and the call lead, through the composed map, to their own C# lines, where the
  interpolated string's raw separators moved both two lines down
