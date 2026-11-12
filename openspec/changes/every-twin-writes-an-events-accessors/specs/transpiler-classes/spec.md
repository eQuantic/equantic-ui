## MODIFIED Requirements

### Requirement: An event's accessors are called as C# calls them

A subscription to an event, `+=`, SHALL run the event's add accessor and an unsubscription, `-=`, its remove
accessor, as C# compiles them: a field-like event's composes its invocation list, and an event declared with `add`
and `remove` accessors, an explicit interface's included, SHALL run its own, whether the subscription binds the
event through its type or through an interface. An event declared with accessors SHALL run its own whatever kind of
type declares it: a class, a static class, a record, a struct or a component. `+=` and `-=` on a delegate, a field,
a local or a property of one, SHALL compose its invocation list as on an event.

#### Scenario: A handler subscribed through an interface's explicit event

- **WHEN** `Bell` implements `event Action IRinger.Rung { add => _rung += value; remove => _rung -= value; }`,
  a handler adding 100 is subscribed through an `IRinger`, and the bell rings
- **THEN** the handler runs, as in .NET

#### Scenario: A field-like event behind the same interface

- **WHEN** `Chime` implements `IRinger.Rung` as `public event Action? Rung;`, and a handler is subscribed through an `IRinger`
- **THEN** the handler runs when the chime rings

#### Scenario: A class event's own accessors

- **WHEN** an event's `add` keeps its handlers in a list and counts them, two handlers are subscribed and one removed
- **THEN** the posts reach the handlers the list holds, and the count is .NET's

#### Scenario: A record's event with accessors

- **WHEN** `record Signal(int Id)` declares `event Action Fired { add => _fired += value; remove => _fired -= value; }`,
  and the code runs `var count = 0; var s = new Signal(1); Action add = () => count++; s.Fired += add;
  s.Fired += () => count += 10; s.Fire(); s.Fired -= add; s.Fire(); return count;`
- **THEN** it answers 21, as in .NET, where it threw

#### Scenario: A struct's event with accessors

- **WHEN** `struct Pulse` declares `event Action<int> Beat` with the same accessors, two handlers adding `n` and
  `n * 10` are subscribed, the pulse fires 2, the first handler is removed, and it fires 3
- **THEN** it answers 52, as in .NET, where it threw

#### Scenario: A component's event with accessors

- **WHEN** a `StatefulComponent` `Meter` declares `event Action<int> Moved` with the same accessors, and the same
  subscriptions, moves and removal run on a `new Meter()`
- **THEN** it answers 52, as in .NET, where it threw
