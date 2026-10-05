# Spec Delta

## ADDED Requirements

### Requirement: A DateTimeOffset's unix times are longs cut toward 0001-01-01

`ToUnixTimeSeconds` and `ToUnixTimeMilliseconds` of a `DateTimeOffset` SHALL answer a `long`, which
the browser holds as a BigInt, and SHALL cut the instant's UTC ticks to whole seconds or
milliseconds counted from 0001-01-01 before the Unix epoch is taken off, as .NET does, so an instant
before 1970 with a fraction of the unit rounds down.

#### Scenario: A fraction before the epoch

- **WHEN** `DateTimeOffset.FromUnixTimeMilliseconds(-999).ToUnixTimeSeconds()` and
  `new DateTimeOffset(new DateTime(621355967999999999), TimeSpan.Zero).ToUnixTimeMilliseconds()` are
  computed
- **THEN** each is -1, as in .NET

#### Scenario: A unix time in long arithmetic

- **WHEN** `DateTimeOffset.FromUnixTimeSeconds(10).ToUnixTimeSeconds() * 1000L` is computed
- **THEN** it is 10000, as in .NET, where a JS number met the long as a TypeError
