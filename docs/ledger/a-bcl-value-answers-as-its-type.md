- **2026-10-30 · A primitive's Equals answers by its type, a pair writes its own text, and a task can be made already ended**:
  bugs under #849 ([#904](https://github.com/eQuantic/equantic-ui/issues/904),
  [#905](https://github.com/eQuantic/equantic-ui/issues/905), [#910](https://github.com/eQuantic/equantic-ui/issues/910),
  [#956](https://github.com/eQuantic/equantic-ui/issues/956), [#957](https://github.com/eQuantic/equantic-ui/issues/957)).
  `1.Equals((object)1.0)` answered True and a byte's or a decimal's `Equals` failed the build: a new strategy answers
  every primitive's `Equals`, reading the other value's type before C# boxed it, and a value already held as `object`
  stays the limit #552 names. A `KeyValuePair` wrote `1,a` where .NET writes `[1, a]`, through every route into text,
  and `Task.FromException` and `Task.FromCanceled` failed the build. A reference tuple was already refused, and a test
  says so now.
