# Design

## How others solve it

Flutter renders no HTML on a server, so it has no row for this in `docs/FLUTTER-PARITY.md`. React
is the comparable case: it hydrates over the server's markup, warns on an attribute mismatch in
development and keeps the server's attribute in production, and asks the page to render the same on
both sides (with a second render after mount for a value only the client knows). That puts the
burden on every page that reads a capability, which is exactly what the Capabilities page promised
the SDK would carry.

## Decision

Hydration is where the server's element and the client's tree meet, so it is where they are made
to agree. For each element it adopts, it hands the reconciler's own `updateAttributes` the
attributes the element has as the old side and the client's as the new one, the same diff a render
uses, so a property-backed attribute (`disabled`, `checked`, `value`) is written the way a render
writes it. Keeping the element, rather than replacing it, is what hydration is for: the listeners it
attached and any focus or scroll the reader already gave the element stay.

The framework's `data-eq-*` markers stay out of the old side when the client does not name them,
because they are written for the client's own sweeps and a server may know one the client's tree
does not repeat.

## The proof

A runtime test per case (an attribute removed, one added, a text corrected, a marker kept), failing
on the old reconciler, and the dashboard sample's recovery codes page in a browser: served
`disabled`, enabled after hydration, and still disabled with the old runtime.
