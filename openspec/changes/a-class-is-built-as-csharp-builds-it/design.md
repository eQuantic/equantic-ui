# Design

## Context

Since #608 a record's and a struct's twin is built as C# builds it: `RecordTypeEmitter` reaches every
C# constructor by the counts of arguments it takes (roots and `: this(…)` alternates), passes a
`: base(…)` chain's and a base clause's arguments as the bound tree binds them (`BoundArguments`),
starts every member in declaration order, evaluates a derived record's initializers before `super()`
and assigns them after, and the construction site applies the object initializer to what the
constructor built (`ObjectInitializer`). All of that is text the record emitter assembles.

A plain class goes through `TypeScriptEmitter`, which writes IR: its constructor is the widest C#
constructor, with a trailing config object assigned at the end, `super()` with no arguments, its
fields' initializers in the constructor and its auto-properties' as class fields. A class field runs
when the class's own constructor starts, or right after `super()` returns in a derived class, so a
property's initializer ran before every field's, and a derived class's after its base's constructor.

## Goals / Non-Goals

**Goals:**

- One constructor builder, written as IR, that the record emitter and the plain class path share, so
  a class is reached, chained, based and started by the same code a record is.
- A class's instance state started in its constructor, in declaration order, never by a class field.

**Non-Goals:**

- A component's constructor. It keeps the runtime's contract (props, services, defaults applied when
  a prop is missing), and its constructors are its factory's, which mirrors one of them.
- A nested class (#584), which is a question of naming every reference to a type, and its own change.
- A base constructor that reads a derived class's member through a virtual member (below).

## Decisions

### One builder, written as IR

The roots, the alternates, the arity tests, the bindings, the chains' arguments, the base call and
the bodies move out of `RecordTypeEmitter` into one class that builds the constructor as a
`JsClassMember` from what the emitter hands it: the declaration, the state to start (each member's
slot, its value and whether its value runs code), the base and its clause, and what runs first (a
static constructor's start). The record emitter writes it in the compact layout it writes every other
member in; the class path adds it to its class. Converted C# reaches it as nodes (an initializer, an
argument, a body), and the statements around them are the IR's own (`If`, `Let`, `Const`, a call to
`super`).

The record's twins are the measure: their pins move by layout only, whitespace stripped they are the
same, and the conformance suite is unchanged. Alternative: a second dispatch for classes in
`TypeScriptEmitter`. Rejected: two copies of the most intricate code in the emitters, each losing the
cases the other fixes, which is how the record path and the class path came to differ in the first
place.

### A class's state lives in its constructor

A plain class's instance fields, auto-properties, the stores of the properties that use `field`, its
instance events and the primary constructor's parameters a member reads are started by the
constructor, in declaration order, each its initializer or its type's default. The class declares
them for TypeScript only (`declare`), as a record does. A class field runs at a moment of its own: at
the start of the class's constructor, or after `super()` returns in a derived class, where it also
writes over whatever the base's constructor set. Alternative: keep the class fields and move only the
property initializers. Rejected: the order of a field and a property would still depend on which kind
each is, and a derived class's fields would still be redefined after `super()`.

### Before `super()`, then after it

C# runs a derived class's initializers before its base's constructor, and JavaScript forbids `this`
before `super()`. As for a record, each initializer that can do anything but read a value is
evaluated into a local before `super()`, in declaration order, and every member is assigned once it
returns. The side effects keep C#'s order. How Flutter answers it: Dart has C#'s order (field
initializers, the initializer list, the superclass constructor, the body), and dart2js keeps it
because it writes constructor functions of its own, not ES classes. eqc writes ES classes, so the
values arrive after `super()`: a base constructor that reads a derived class's member through a
virtual member reads it unset, where .NET reads it initialized. No twin can reach that value before
`super()` returns, so it is documented on the wiki's SupportedFeatures page as the one difference.

### Built, then initialized

`new C(…) { … }` over a plain class whose twin eqc writes is the record's construction: the
constructor the call binds, the arguments as the bound tree binds them, then the initializer applied
by `ObjectInitializer`. The trailing config object stays where a class takes one: a component's props
and the vocabulary's hand-written twins. An exception is built by its symbol (`$eq.exceptions.create`),
and its initializer is applied to what that builds, through the same `ObjectInitializer`.

### A held parameter and a member of one name

A primary constructor's parameter a member reads is held on the instance under its camelCased name,
as a struct's is since #608, and a held parameter that lands on another member's name is EQ1007, as
it is for a struct. Alternative: hold it under a name no member can take, as C# holds it in a hidden
field. Better, and it would lift the struct's refusal too, but the component path holds its own
parameters the same way and the hydration payload names them, so it is a change of its own.

## Risks / Trade-offs

- [The record pins move] → each moved pin is compared with whitespace stripped before it is
  regenerated, and the conformance suite runs on both sides unchanged.
- [Hand-written TypeScript that built a transpiled class with a config object] → none in the
  runtime: its specs build those classes with positional arguments. The migration line names it.
- [A class's state no longer a class field] → a member read before the constructor starts it reads
  undefined; C# reads its default there only from a base constructor's virtual call, the documented
  difference above.
