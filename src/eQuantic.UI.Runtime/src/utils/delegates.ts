/**
 * C# MULTICAST delegates, which JavaScript does not have.
 *
 * `form.Changed += handler` is not addition. C# composes an invocation list, and `+=` emitted
 * literally became `null + function` — a STRING — so the next `changed?.()` threw
 * "this.changed is not a function", from inside a component, on the client only. The server was
 * running the same C# correctly the whole time.
 *
 * These two are what the emitter calls instead. The composed function keeps C# order (handlers run
 * in the order they were added) and `remove` takes the LAST occurrence, which is what
 * `Delegate.Remove` does when the same handler was added twice.
 */

/* eslint-disable @typescript-eslint/no-unsafe-function-type -- a delegate IS any function: these only
   store, combine and spread them, never call one with arguments of their own. */

/** The invocation list of a composed delegate, kept so `remove` can rebuild it. */
const lists = new WeakMap<Function, Function[]>();

const listOf = (delegate: Function | null | undefined): Function[] =>
  delegate ? (lists.get(delegate) ?? [delegate]) : [];

const compose = (handlers: Function[]): Function | null => {
  if (handlers.length === 0) return null;
  if (handlers.length === 1) return handlers[0];
  const composed = (...args: unknown[]) => {
    let result: unknown;
    // Every handler runs, and the LAST one's value is the delegate's — C#'s own rule for a
    // multicast delegate with a return type.
    for (const handler of handlers) result = handler(...args);
    return result;
  };
  lists.set(composed, handlers);
  return composed;
};

/** `delegate += handler` — appends, preserving order. */
export function combineDelegate(
  delegate: Function | null | undefined,
  handler: Function | null | undefined,
): Function | null {
  if (!handler) return delegate ?? null;
  return compose([...listOf(delegate), handler]);
}

/** `delegate -= handler` — drops the LAST occurrence, like `Delegate.Remove`. */
export function removeDelegate(
  delegate: Function | null | undefined,
  handler: Function | null | undefined,
): Function | null {
  if (!delegate || !handler) return delegate ?? null;
  const handlers = [...listOf(delegate)];
  const at = handlers.lastIndexOf(handler);
  if (at < 0) return delegate;
  handlers.splice(at, 1);
  return compose(handlers);
}
