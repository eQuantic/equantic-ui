## ADDED Requirements

### Requirement: A field a case apart from a member keeps its own slot

A plain class's instance field whose twin name is another member's, a property, a method, an event or another field of its type or its bases, SHALL live in a slot of its own, its name with a `$` after it, and a slot an ancestor already holds SHALL be taken whatever the spellings, so a field that would meet one SHALL take a `$` more until its slot is free. Every read and write of the field SHALL reach that slot, a call of a delegate field, a read of a field called `Count` and a pattern's included: a property subpattern SHALL read the member the model binds it to, each one along an extended path, and a positional pattern over a `Deconstruct` the app wrote SHALL call it and read the parts it hands back, never the members its outs are named after, and a pattern-matching operation SHALL call it once for each value, as .NET does, across the arms of a switch and the alternatives of an `or`, in an initializer too. The other member SHALL keep its name. A class with such a field SHALL be written to JSON as System.Text.Json writes it: the property, read through its getter once, and never the field.

#### Scenario: A setter beside its field

- **WHEN** `int value;` stands beside `int Value { get => value; set => this.value = value * 2; }`, and `Value` is set to 3
- **THEN** `Value` reads 6, as in .NET

#### Scenario: A method beside a field

- **WHEN** `int size = 3;` stands beside `int Size() => size * 2`, and `Size()` is called
- **THEN** it answers 6, as in .NET

#### Scenario: A pattern naming the field

- **WHEN** `int value = 1;` stands beside `int Value => value * 10;`, and `this is { value: 1 }` is tested
- **THEN** it answers true, as in .NET, and so does a switch arm or a case label naming the field, `this is { value: var v }` binds 1, and `this is { next.value: 1 }` reads each field on its path in its slot

#### Scenario: A positional pattern through a Deconstruct the app wrote

- **WHEN** `Point` holds the fields `x` and `y` beside `int X => x * 10` and `int Y => y * 10`, its `Deconstruct(out int x, out int y)` hands back the fields, and `new Point(1, 2) is (1, 2)` is tested
- **THEN** it answers true, as in .NET: the pattern calls the `Deconstruct`, an extension's as well, and reads the parts it hands back

#### Scenario: A method forwarding to its delegate field

- **WHEN** `Func<int, bool> validate = n => n > 2;` stands beside `bool Validate(int n) => validate(n)`, and `Validate(3)` is called
- **THEN** it calls the delegate and answers true, as in .NET, and so does a call of the field through `this`, on another instance and through `?.`

#### Scenario: A field called Count

- **WHEN** `int Count = 2;` stands beside `int count() => Count * 3`, and `t.Count + t.count()` is read
- **THEN** it answers 8, as in .NET: the field is read in its slot, not as a collection's count

#### Scenario: A derived field beside an inherited one

- **WHEN** a class with `int value = 3;` derives from one with `public int Value = 2;`, or one with `int value = 4;` derives from one whose own `value` moved beside its property `Value`
- **THEN** each field keeps its own value, as in .NET: the derived field takes a `$` more than every slot its ancestors hold

#### Scenario: A Deconstruct with an effect

- **WHEN** a `Deconstruct` the app wrote counts its calls, and `new Tracked(1, 2) is (1, 2)` initializes a field, or a switch tests `(1, _)` and then `(var a, var b)`, or an `is` tests `(1, _) or (_, 4)`
- **THEN** each test calls it once, as in .NET

#### Scenario: The JSON of a class with a moved field

- **WHEN** an instance whose field moved is written to JSON
- **THEN** the property is written under its name, read through its getter once even where a moved field and the property's store both stand for it, and the field's slot is not written
