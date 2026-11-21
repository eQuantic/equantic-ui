# Proposal

Closes #1779 and #1781, two sub-issues of the epic #1774 ("Lessons from a real site: what fails only in
the browser is caught by the build and the tests").

## Why

Measured on `main`, in the dashboard sample's real build:

- **Server code needed a mark, class by class.** A middleware no component names failed the build with
  EQ2112 and EQ2004 at lines nothing in the browser runs, and a `[GeneratedRegex]` in a `[ServerOnly]`
  class failed it with about twenty-five EQ2004 inside the regex generator's own file. A referenced
  project's type is a module only when a written module imports it (#624); the app's own types were all
  modules, whatever they were.
- **A component's static helper could not be called from another namespace.** The generated factory comes
  in through a `global using static`, and a file's `using Shop.Components;` at its top stands at the same
  level: between the method and the type, C# takes the method, and `ProductEditor.Toggled(true)` was CS0119.
  In the component's own namespace it compiled, which is why the wiki said "your own components are never
  shadowed".

## What Changes

- **A class, a record, a static class or a generated type of the app's is a module only when a component
  reaches it.** The roots are the app's components (pages and the layout among them, wherever their files
  sit); from them the build follows each module's imports of its sibling modules, the edges the bundler
  follows. What is never reached is server code by inference: no module is written for it and nothing it
  reported is said. A component that names such a type reaches it and gets its diagnostics where they are.
  A component that runs only on the server is still marked `[ServerOnly]`.
- **A component that declares a static member gets a `global using` alias** beside its factory, which
  outranks both the factory and a file's `using` of its namespace, so its helpers and constants read from
  anywhere in the app while a call of the name still reaches the factory. No alias is written for a
  component named like a type a file of the app imports from another namespace at the compilation unit's
  level (`Timer` beside `System.Threading`), nor like a member of the global namespace, so no name an app
  writes changes meaning.

The issue's third proposal, marking a folder or a namespace server-only once, is not added: a type no
component reaches needs no mark now, and one a component reaches must not carry one.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `client-boundary`: a type of the app no component reaches is server code.
- `component-factories`: a component's statics are read from another namespace.

## Impact

- eqc (`AppBuild`, `AppModules`, `CompilationResult.IsComponent`) and the web SDK's generator
  (`AppFactorySurfaceGenerator`, `ComponentAliases`).
- Neither the public surface nor the developer surface moves (`IsComponent` is internal; the generator is a
  tool).
- No break: the dashboard sample's served JavaScript is byte for byte what `main` serves. What changes is
  that an unreached type's diagnostics are no longer said, and that a component with statics is aliased.
- Rules followed: 2 (the app's case is one test of a general rule, the referenced projects' rule applied to
  the app), 9 (C#'s own precedence of an alias, rather than a factory under another name), 13.
