## Why

Closes #1827, a Bug under #1587.

A component whose constructor takes a dependency, an interface the container gives it, lost its object initializer
in the browser. An argument it omitted after the dependency also arrived as `null` instead of its default:

| C# | .NET reads | the twin was built |
|---|---|---|
| `new Quark(_clock, mood: "x") { Size = 3 }` | `Mood = "x"`, `Size = 3` | `new Quark('x', 'calm', { size: 3 })` |
| `new Quark(_clock) { Size = 5 }` | `Mood = "calm"`, `Size = 5` | `new Quark(null, 'calm', { size: 5 })` |

The twin's constructor leaves the dependency out of its signature and resolves it in its body, and the call drops
the dependency's argument with it. The config path then filled the remaining parameters' defaults up to the C#
constructor's arity (`FillToArity`, #1756), which still counted the dependency. So one default too many went
before the config, and the config landed past the twin's last parameter. With nothing written after the
dependency, the defaults started at the dependency's own, `null`.

## What Changes

For a developer using the SDK: `new Quark(_clock, mood: "x") { Size = 3 }` builds the component .NET builds, with
its argument in place and its initializer applied, whichever way the arguments are written.

- **One rule says which parameters the twin keeps.** `LeavesOut` answers whether the twin's constructor leaves a
  parameter out: a dependency of a component that declares one. Both the arguments a call passes and the defaults
  before its config are counted by it, so the two can no longer disagree. Before, the drop asked
  `CapabilityRule.IsDependency` in place and the defaults asked nothing.
- **The defaults before a config are counted over the kept parameters.** `FillToArity` fills from the twin's own
  parameter list, so `new Quark(_clock) { Size = 5 }` is `new Quark('calm', { size: 5 })`.

Parts reached: eqc only (`ObjectCreationStrategy`). No public surface and no developer surface moves; a
construction of a type with no dependency is unchanged, and the shared components the runtime ships are unchanged
byte for byte.

Rules of `docs/PRINCIPLES.md` followed: one rule, one home. The question "does the twin keep this parameter" had
two answers, one at the drop and none at the fill, and now has one.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `transpiler-classes`: one new requirement, a component's dependency leaves no slot before its initializer.

## Impact

`tests/eQuantic.UI.Conformance.Tests` gains `DependencyBesideAnInitializerConformanceTests`, five constructions read
on both sides in both modes. `ConstructorInjectionTests` in `tests/eQuantic.UI.Web.Tests` gains a pin of the
construction the twin is built with for four of them.
