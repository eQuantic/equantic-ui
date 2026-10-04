/**
 * The value `new object()` makes: nothing but an identity. A plain `{}` is an anonymous type here,
 * compared by its members, so every two of them were equal, as `Equals` and as dictionary keys.
 */
export class NetObject {
  /** `object.Equals`: the same object, and no other. */
  equals(other: unknown): boolean {
    return this === other;
  }

  /** `object.ToString()`: the type's name. */
  toString(): string {
    return 'System.Object';
  }
}

/** `new object()`. */
export function newObject(): NetObject {
  return new NetObject();
}

/**
 * A `lock` statement's gate, evaluated once before its body as C# evaluates it, and refused when null,
 * as `Monitor.Enter` refuses it: nothing is held, since a page has one thread, but a null gate let the
 * body run where .NET throws before it.
 */
export function lockGate(gate: unknown): void {
  if (gate == null) throw new Error("Value cannot be null. (Parameter 'obj')");
}
