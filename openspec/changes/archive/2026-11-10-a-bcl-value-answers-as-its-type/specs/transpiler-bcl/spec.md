## ADDED Requirements

### Requirement: A primitive's Equals answers by its own type

`Equals` on `bool`, `char`, every integer width, `float`, `double` and `decimal`, typed or taking `object`, SHALL build, and `Equals(object)` SHALL be true only for an equal value of the receiver's own type, with both values evaluated, the receiver first. The other value's type SHALL be read from the bound tree before C# boxed it. A `double` and a `float` SHALL find NaN equal to NaN. A value already held as `object` SHALL compare by what it holds, where an int and a double of one value, or a char and a text of one, cannot be told apart.

#### Scenario: A value of another type

- **WHEN** a program evaluates `1.Equals((object)1.0)`, `'a'.Equals((object)"a")` or `1.Equals((object)1L)`
- **THEN** the answer is False, as in .NET

#### Scenario: Every primitive builds

- **WHEN** a program evaluates `((byte)1).Equals((object)(byte)1)`, `1m.Equals((object)1m)` or `double.NaN.Equals((object)double.NaN)`
- **THEN** the build succeeds and the answer is True, as in .NET

#### Scenario: Both values are evaluated

- **WHEN** a program evaluates `A().Equals((object)B())`, where `A` returns an int and `B` a double
- **THEN** `A` runs, then `B`, and the answer is False

### Requirement: A KeyValuePair writes its text as .NET does

A `KeyValuePair<TKey, TValue>` SHALL write `[key, value]` through a hole, a concatenation, `ToString`, `string.Join`, `string.Format`, `StringBuilder.Append` and a `ToString` method group, each half as its type writes it in the culture in force, a null one as nothing.

#### Scenario: Each route into text

- **WHEN** a program writes `new KeyValuePair<int, string>(1, "a")` into any of those routes
- **THEN** the text is `[1, a]`

#### Scenario: Held as object

- **WHEN** a program writes a pair held in an `object` into a hole
- **THEN** the text is `[1, a]`

### Requirement: A task already ended is read by whoever awaits it

`Task.FromException` and `Task.FromCanceled`, generic or not, SHALL build. An `await` of the first SHALL throw its exception, and of the second a `TaskCanceledException`. A null exception and a token not cancelled SHALL be refused at the call in .NET's words. A task of either kind nobody awaits SHALL report nothing.

#### Scenario: An awaited faulted task

- **WHEN** a program awaits `Task.FromException(new InvalidOperationException("boom"))`
- **THEN** the `await` throws that exception

#### Scenario: A token not cancelled

- **WHEN** a program calls `Task.FromCanceled(CancellationToken.None)`
- **THEN** the call throws `ArgumentOutOfRangeException` naming `cancellationToken`

### Requirement: A reference tuple is refused by the build

`Tuple.Create` and `new Tuple<…>` SHALL fail the build with EQ2004 naming the member.

#### Scenario: Tuple.Create

- **WHEN** a client component calls `System.Tuple.Create(1, "a")`
- **THEN** the build fails with EQ2004
