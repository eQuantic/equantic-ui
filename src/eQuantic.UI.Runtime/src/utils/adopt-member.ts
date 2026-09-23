/**
 * How a member that arrived from the WIRE becomes a property of the value built from it: one rule
 * for every path that rebuilds a value from a payload (a typed spec, a record twin, the witness
 * path, a dictionary, a component's own fields).
 *
 * `target[key] = value` was wrong twice for a key the payload chose:
 * - The server writes every public property, the COMPUTED ones too, so a `Rect` arrives with
 *   `left`, `right`, `center` and `isEmpty` beside its four fields. The twin declares those as
 *   getters, and assigning a member that only has a getter throws in a module: hydration failed on
 *   the payload the server actually writes. A member the twin DERIVES is skipped, and its getter
 *   answers from the members that did arrive.
 * - `__proto__` is an accessor on `Object.prototype`. Assigning it swapped the prototype of the
 *   value being rebuilt for whatever object the payload held, and a dictionary entry under that key,
 *   which a page's data is free to hold, was dropped. A key is DEFINED as the value's own member,
 *   which is what JSON itself made of it.
 *
 * A member with a SETTER still goes through it: that is how a property with a backing field stores
 * what it is given.
 *
 * A leaf module, so both hydration modules and the component read it without importing each other.
 */
export function adoptMember(target: object, key: string, value: unknown): void {
  const accessor = declaredAccessor(target, key);
  if (accessor) {
    if (accessor.set) (target as Record<string, unknown>)[key] = value;
    return;
  }
  Object.defineProperty(target, key, {
    value,
    writable: true,
    enumerable: true,
    configurable: true,
  });
}

/**
 * Whether the value itself declares `key`: an own field, or an accessor its class declares. What
 * every object inherits from `Object.prototype` (`__proto__`, `constructor`, `toString`) is not a
 * member of the value, and a payload that names one is not naming a field.
 */
export function declaresMember(target: object, key: string): boolean {
  return (
    Object.prototype.hasOwnProperty.call(target, key) || declaredAccessor(target, key) !== undefined
  );
}

/** The accessor `key` reaches on the value's own class chain, which stops short of Object.prototype. */
function declaredAccessor(target: object, key: string): PropertyDescriptor | undefined {
  for (
    let proto = Object.getPrototypeOf(target) as object | null;
    proto !== null && proto !== Object.prototype;
    proto = Object.getPrototypeOf(proto) as object | null
  ) {
    const own = Object.getOwnPropertyDescriptor(proto, key);
    if (own) return own.get || own.set ? own : undefined;
  }
  return undefined;
}
