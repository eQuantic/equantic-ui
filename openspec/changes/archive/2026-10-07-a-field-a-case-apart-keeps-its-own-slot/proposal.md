# Proposal

Closes #396, a Bug under #565 (The transpiler's fences hold on every path, continued).

## Why

A JavaScript object has one member per name, and eqc names a member by its name camel-cased, so a C# field and a property or a method a case apart became one member of the twin. It is the shape a C# class has most: a field beside the property that exposes it. Measured on main fcae8f29 through the module graph an app's build writes:

- `int value;` beside `int Value { get => value; set => this.value = value * 2; }`: the own field hid the accessors, the setter never ran, and `Value` read 3 where .NET reads 6,
- `string name = "ana";` beside the auto-property `Name = "bia"`: one slot, which the second initializer won,
- `int size = 3;` beside `int Size() => size * 2`: the call reached the number and threw,
- a field beside a base's property, and another instance's field read through `other.value`.

Since #608 and #621, a record, a struct and a component refuse such a pair with EQ1007, which names both members. A plain class built with no diagnostic and failed in the browser alone.

## What Changes

- **A plain class's instance field a case apart from another member moves to a slot of its own**: its name with a `$` after it (`value$`), which no C# name holds and which a property's own store (`$name`) never meets. The property, the method or the event keeps the name callers read. Of two fields a case apart, the one the casing changed moves. Its base's members count too.
- **One rule names the slot** (`FieldSlotExtensions.TwinSlot`), which the class's instance state, a bare field read, a member access and a null-conditional assignment's target all read.
- **A class with a moved field answers `toJSON` with `$eq.json`**, and the runtime's `twinJson` writes no `name$` key: it is storage the serializer never writes, and the property it gave its name to is written instead, read through its getter, as System.Text.Json reads it.

What does not move: a component, a record and a struct keep EQ1007 for the same pair. A component's fields cross to the browser by name in the hydration manifest, and a record's members are its equality, its printing and its `with`, so renaming one there has to reach those too.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `transpiler-classes`: a field a case apart from a member keeps its own slot.
