# Spec Delta

## ADDED Requirements

### Requirement: Task.Delay waits as .NET waits

`Task.Delay` SHALL wait for a `TimeSpan` as for a number of milliseconds, a fraction of a millisecond
cut, `-1` and `Timeout.InfiniteTimeSpan` forever. It SHALL throw `ArgumentOutOfRangeException` before
anything else for a delay below -1, or past 4294967294 ms for a `TimeSpan`, with .NET's message and
parameter name (`millisecondsDelay`, `delay`). Given a token, the task SHALL end as cancelled, an
`await` throwing `TaskCanceledException` that carries the token, as soon as the token is cancelled,
and at once when it already is.

#### Scenario: A TimeSpan

- **WHEN** `await Task.Delay(TimeSpan.FromMilliseconds(40))` is timed
- **THEN** it waits at least 40 ms, as in .NET

#### Scenario: A token cancelled during the wait

- **WHEN** `await Task.Delay(1000, source.Token)` runs and the source is cancelled after 30 ms
- **THEN** the await throws a `TaskCanceledException` whose `CancellationToken` is the token

#### Scenario: A delay below -1

- **WHEN** `Task.Delay(-2)` is called
- **THEN** it throws an `ArgumentOutOfRangeException` for `millisecondsDelay`, with .NET's message
