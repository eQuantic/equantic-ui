# Proposal

Closes #1140, a Bug under #849 (the transpiler's fences hold on every path, part three).

## Why

Whether a method stays on the server was asked of its syntax alone: `ComponentParser.StaysOnTheServer`
read the method's own attribute lists for the name `ServerOnly`. Two spellings C# accepts carry the
attribute where those lists do not show it, and in both the method was written into its type's twin,
body and signature included:

- an alias, `using Keep = eQuantic.UI.Primitives.ServerOnlyAttribute;` then `[Keep] public string Read() { … }`;
- a partial method whose defining half carries `[ServerOnly]` and whose implementing half, the one with
  the body, does not. C# merges the attributes of the two halves on the method's symbol.

Measured on main (6519dd94e) for a class, a static class, a record, a struct, a static class nested in a
component and a component, with eqc's compilation of the project and with a standalone `CompileSource`:
every case wrote the method into the twin, named the app type its body constructs, and failed the build
with EQ2112 and EQ2103, the refusal of the file access and the `HttpClient` the method was marked to keep
out of the browser. The same syntax-only rule made the EQ1007 overload guard count such a method, so a
server-only overload beside one that crosses was refused as two methods on one name.

The attribute written on the method, as written, has been left out by every emitter since #816 and #944.

## What Changes

For a developer, `[ServerOnly]` on a method means what it says however it is spelled: through an alias,
on either half of a partial method, bare, with its suffix or qualified. Such a method is no member of
any twin, the module neither imports nor refuses what it names, client code that calls it is refused
where it calls it (EQ2010, as before), and it takes no name the EQ1007 guard counts.

- **eqc**: `ComponentParser.StaysOnTheServer` takes the model of the compilation that holds the node and
  asks the method's symbol; the attribute lists are read by name only where no model holds the tree.
  Every reader of the rule hands it its model: the parser's component path, the class, static-class and
  record emitters (methods, operators, conversions, interface defaults), method lowering (extension
  blocks), the import and refusal scans (`NamedSourceTypes`, `RuntimeProvidedTypeScanner`,
  `ServerOnlyContracts`) and the EQ1007 guard (`OverloadedMethods`).
- No other part moves: the runtime, the realizers, the shells, the SDKs and the templates are untouched.
- The public surface does not move (`StaysOnTheServer` is internal), nor does the developer surface.

## Capabilities

### Modified Capabilities

- `client-boundary`: a method is server-only when its symbol says so.

## Impact

`src/eQuantic.UI.Compiler` only, and its tests (`ServerOnlyMethodBySymbolTests`, `OverloadedMethodTests`).
A member that writes no attribute and is no partial method is answered without binding, so the scans
that ask the rule of every node bind nothing more than they did.
