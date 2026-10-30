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

const bounds = new WeakMap<object, Map<Function, Function>>();
const overs = new WeakMap<Function, Function>();

/**
 * The delegate of `method` over `target`. .NET compares two delegates by their target and their method, and
 * this side compares functions by identity, so there is one function for a target and a method: `==`,
 * `Equals` and `-=` find a handler made from the group again (#959). `object.ReferenceEquals` of two such
 * delegates is true here and false in .NET. The entry lives as long as its target does.
 */
export function boundDelegate<T extends Function>(target: object, method: T): T {
  // A value the browser holds as a primitive is no key, and carries no identity to compare by.
  if (typeof target !== 'object' && typeof target !== 'function') return method.bind(target) as T;
  let methods = bounds.get(target);
  if (methods === undefined) bounds.set(target, (methods = new Map()));
  let bound = methods.get(method);
  if (bound === undefined) methods.set(method, (bound = method.bind(target) as Function));
  return bound as T;
}

/** A delegate built from `source`, which `make` writes over it: one for a source, and never the source itself,
 * so two built from one are equal and neither equals it, as .NET finds them. */
export function delegateOver<T extends Function>(source: T, make: (source: T) => T): T {
  let over = overs.get(source);
  if (over === undefined) overs.set(source, (over = make(source)));
  return over as T;
}

/**
 * `delegate += handler` — appends, preserving order. Typed by the delegate's own type, so an event
 * field declared as a function of its arguments takes the composed delegate back, and the handler
 * written in place is typed from it: an untyped answer was not assignable to the field it came from.
 */
export function combineDelegate<T extends Function>(
  delegate: T | null | undefined,
  handler: T | null | undefined,
): T | null {
  if (!handler) return delegate ?? null;
  return compose([...listOf(delegate), handler]) as T | null;
}

/** `delegate -= handler` — drops the LAST occurrence, like `Delegate.Remove`. */
export function removeDelegate<T extends Function>(
  delegate: T | null | undefined,
  handler: T | null | undefined,
): T | null {
  if (!delegate || !handler) return delegate ?? null;
  const handlers = [...listOf(delegate)];
  const at = handlers.lastIndexOf(handler);
  if (at < 0) return delegate;
  handlers.splice(at, 1);
  return compose(handlers) as T | null;
}
