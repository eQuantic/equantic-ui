- **2026-11-11 · An event declared with accessors lowers to them, and += composes a delegate**: a bug under #849
  ([#746](https://github.com/eQuantic/equantic-ui/issues/746)). The emitter knew only field-like events: an event
  declared with `add` and `remove` had no twin, and a subscription wrote a key of its name the accessors never read,
  so a handler subscribed through an interface's explicit event was never raised (.NET 105, the browser 5). Such an
  event is now two methods of its twin, `$add$` and `$remove$` with its name, a subscription calls what the event it
  binds has, and the runtime chooses for an interface's, an abstract or a virtual one. Beneath it, `+=` on a delegate
  field was emitted literally, so the field held the text of its handlers; it now composes the invocation list, as
  on an event. Proposed and archived through OpenSpec (`transpiler-classes`).
