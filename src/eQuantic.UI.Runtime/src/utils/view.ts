/**
 * The live views a dictionary answers for its keys and its values: .NET's `KeyCollection` and
 * `ValueCollection`, a sorted dictionary's two and a sorted list's `KeyList` and `ValueList` (#463,
 * #864). Each reads its collection when it is read, as .NET's do, where an array copied when `Keys`
 * was read went on holding what the collection held then.
 */

/** The array methods that change an array, which a view refuses. */
const MUTATORS: ReadonlySet<string> = new Set(['push', 'pop', 'shift', 'unshift', 'splice', 'sort', 'reverse', 'fill', 'copyWithin']);

/** Every view {@link liveView} made, and what it refuses a change with. */
const refusals = new WeakMap<object, () => never>();

/** What a view reads its collection through, and what it refuses a change with. */
export interface ViewSource<T> {
  /** The elements now, in the collection's order, copied into the view's snapshot whenever
   *  {@link revision} has moved. */
  read(): T[];
  /** A number that moves whenever {@link read} would answer differently. */
  revision(): number;
  /** How many elements there are, read without a snapshot. */
  count(): number;
  /** The walk, which refuses a change as the collection's own enumerator refuses it. */
  walk(): Iterator<T>;
  /** `Contains` by the collection's own comparison, where the view answers it so (a key collection). */
  contains?: (value: T) => boolean;
  /** What a change asked of the view throws: a CALL (`Add`, `Remove`, `Clear`, which lower to the
   *  array's methods and to a write of its length) or an ELEMENT written through the indexer, which
   *  .NET's sorted list refuses in other words than the call. */
  refusal(change: 'call' | 'element'): Error;
}

/**
 * A view that reads its collection when it is read. It is an array to everything a lowering hands it
 * to, `length`, an index, the array methods and `JSON.stringify` alike, through a Proxy over a frozen
 * snapshot taken again whenever the collection changed, so reading it costs a copy once per change and
 * never per element. Read-only, as .NET's views of a dictionary are: a change asked of it throws.
 */
export function liveView<T>(source: ViewSource<T>): T[] {
  let snapshot: readonly T[] = [];
  let seen = Number.NaN;
  const current = (): readonly T[] => {
    const revision = source.revision();
    if (seen !== revision) {
      snapshot = Object.freeze(source.read());
      seen = revision;
    }
    return snapshot;
  };
  const walk = (): Iterator<T> => source.walk();
  const refuseCall = (): never => {
    throw source.refusal('call');
  };
  const view = new Proxy([] as T[], {
    get: (_target, property) => {
      if (property === Symbol.iterator) return walk;
      if (property === 'contains' && source.contains !== undefined) return source.contains;
      // ICollection<T>.IsReadOnly: a dictionary's views are, as .NET's answer, where it read undefined
      // and a caller that asked before changing one went on to change it.
      if (property === 'isReadOnly') return true;
      // The count without a snapshot: a loop that changes the collection and reads the count each
      // pass would copy every element each time.
      if (property === 'length') return source.count();
      // A method that would change the view is refused as .NET's views refuse ICollection<T>'s Add,
      // Remove and Clear, where the frozen snapshot threw a TypeError, read as a NullReferenceException.
      if (typeof property === 'string' && MUTATORS.has(property)) return refuseCall;
      const array = current();
      // An element as it is, a delegate held as a value included: only the array's own methods are
      // bound to the snapshot they read, where every function was, and a delegate read back was
      // another one.
      if (Object.prototype.hasOwnProperty.call(array, property)) return Reflect.get(array, property, array);
      const value: unknown = Reflect.get(array, property, array);
      return typeof value === 'function' ? value.bind(array) : value;
    },
    has: (_target, property) => Reflect.has(current(), property),
    ownKeys: () => Reflect.ownKeys(current()),
    // An index is reported configurable, as a proxy must report a property its target lacks, and the
    // length as the target's own is, with the view's count.
    getOwnPropertyDescriptor: (target, property) => {
      const array = current();
      if (property === 'length') return { ...Reflect.getOwnPropertyDescriptor(target, 'length'), value: array.length };
      const own = Reflect.getOwnPropertyDescriptor(array, property);
      return own === undefined ? undefined : { ...own, configurable: true };
    },
    // `Clear` writes the length; anything else written is an element.
    set: (_target, property) => {
      throw source.refusal(property === 'length' ? 'call' : 'element');
    },
  });
  refusals.set(view, refuseCall);
  return view;
}

/**
 * Throws what a view refuses a change with when `value` is one {@link liveView} made: `ICollection<T>`'s
 * `Remove`, which .NET's views refuse whether or not they hold the value, where the search a list's
 * removal starts with answered false for one they did not (found in review, #864).
 */
export function refuseAChangeOf(value: unknown): void {
  if (typeof value === 'object' && value !== null) refusals.get(value)?.();
}
