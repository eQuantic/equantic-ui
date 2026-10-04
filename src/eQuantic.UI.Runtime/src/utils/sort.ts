/**
 * .NET's sort, step for step: INTROSPECTIVE (a quicksort around a median-of-three pivot, an insertion
 * sort for a partition of 16 or fewer, and a heap sort past twice the depth of a balanced tree) and
 * UNSTABLE, so equal elements land where .NET's land. `Array.prototype.sort` is stable and compares
 * the elements' TEXT unless handed a function: `[10, 9, 1].sort()` is `1, 10, 9` (#488).
 *
 * Two helpers, as .NET has two, and the compiler says which a call takes ({@link SortOrder}):
 *  - `ArraySortHelper<T>`, by a comparison: a `Comparison<T>`, an `IComparer<T>`, and the default
 *    comparer of a type that is not `IComparable<T>` (an enum, a `Nullable<T>`). Its partition reads
 *    past the span when the comparison is inconsistent, which .NET reports as an `ArgumentException`
 *    naming the comparer;
 *  - `GenericArraySortHelper<T>`, by the type's own `CompareTo`, for the default comparer of an
 *    `IComparable<T>`: a null orders first without being asked, and a `double` or a `float` has its
 *    NaNs moved to the front before the sort, which is where a `-0` beside a `0` ends up decided.
 *
 * Whatever a comparison throws is wrapped as .NET wraps it, in an `InvalidOperationException` that
 * says two elements failed to compare, the thrown one as its `cause` (.NET's `InnerException`).
 */

/** How a sort compares, and which of .NET's helpers it is: see the module's description. */
export interface SortOrder<T> {
  /** The comparison; null for a type .NET's default comparer cannot order, which throws when asked. */
  readonly compare: ((a: T, b: T) => number) | null;
  /** `'comparer'`: ArraySortHelper; `'comparable'`: GenericArraySortHelper; `'real'`: the same, for a
   *  double or a float, its NaNs moved to the front first. */
  readonly kind: 'comparer' | 'comparable' | 'real';
  /** What .NET's message about an inconsistent comparer names: the comparer's `ToString`. */
  readonly name: string;
}

/** Past this many, a partition is quick-sorted; up to it, insertion-sorted (`Array.IntrosortSizeThreshold`). */
const SIZE_THRESHOLD = 16;

/** A read past the partition, which .NET's span reports as an `IndexOutOfRangeException`. */
class OutOfRange extends Error {}

/** .NET's `InvalidOperationException` for a comparison that threw, carrying what it threw. */
export function compareFailed(cause: unknown): Error {
  const error = new Error('Failed to compare two elements in the array.');
  Object.defineProperty(error, 'cause', { value: cause, configurable: true, writable: true });
  return error;
}

/** The comparison of an order, which for a type .NET cannot order throws as its default comparer does. */
export function comparisonOf<T>(order: SortOrder<T>): (a: T, b: T) => number {
  return order.compare ?? uncomparable;
}

function uncomparable(): number {
  throw new Error('At least one object must implement IComparable.');
}

/**
 * Sorts `keys[index, index + count)` in place as .NET's `ArraySortHelper<T>.Default.Sort` (or, for a
 * `Comparison<T>`, `ArraySortHelper<T>.Sort`) sorts that span.
 */
export function introSort<T>(keys: T[], index: number, count: number, order: SortOrder<T>): void {
  if (count < 2) return;
  const compare = comparisonOf(order);
  try {
    if (order.kind === 'comparer') {
      comparerSort(keys, index, count, 2 * (log2(count) + 1), compare);
      return;
    }
    let from = index;
    let length = count;
    if (order.kind === 'real') {
      const nans = moveNansToFront(keys as unknown as number[], index, count);
      if (nans === count) return;
      from += nans;
      length -= nans;
    }
    comparableSort(keys, from, length, 2 * (log2(length) + 1), compare);
  } catch (error) {
    if (error instanceof OutOfRange) {
      throw new Error(
        'Unable to sort because the IComparer.Compare() method returns inconsistent results. Either a value does not compare equal to itself, or one value repeatedly compared to another value yields different results. ' +
          `IComparer: '${order.name}'.`,
      );
    }
    throw compareFailed(error);
  }
}

/** `BitOperations.Log2` of a positive int. */
function log2(value: number): number {
  return 31 - Math.clz32(value);
}

function swap<T>(keys: T[], i: number, j: number): void {
  const t = keys[i];
  keys[i] = keys[j];
  keys[j] = t;
}

/** `SortUtils.MoveNansToFront`: each NaN swapped to the front, in order; answers how many there are. */
function moveNansToFront(keys: number[], index: number, count: number): number {
  let left = 0;
  for (let i = 0; i < count; i++) {
    const value = keys[index + i];
    if (value !== value) {
      swap(keys, index + left, index + i);
      left++;
    }
  }
  return left;
}

// ---- ArraySortHelper<T>: a comparison ------------------------------------------------------------

function comparerSort<T>(keys: T[], lo: number, size: number, depthLimit: number, compare: (a: T, b: T) => number): void {
  let partitionSize = size;
  while (partitionSize > 1) {
    if (partitionSize <= SIZE_THRESHOLD) {
      if (partitionSize === 2) {
        swapIfGreater(keys, compare, lo, lo + 1);
        return;
      }
      if (partitionSize === 3) {
        swapIfGreater(keys, compare, lo, lo + 1);
        swapIfGreater(keys, compare, lo, lo + 2);
        swapIfGreater(keys, compare, lo + 1, lo + 2);
        return;
      }
      insertionSort(keys, lo, partitionSize, compare);
      return;
    }
    if (depthLimit === 0) {
      heapSort(keys, lo, partitionSize, compare);
      return;
    }
    depthLimit--;
    const p = pickPivotAndPartition(keys, lo, partitionSize, compare);
    comparerSort(keys, lo + p + 1, partitionSize - (p + 1), depthLimit, compare);
    partitionSize = p;
  }
}

function swapIfGreater<T>(keys: T[], compare: (a: T, b: T) => number, i: number, j: number): void {
  if (compare(keys[i], keys[j]) > 0) swap(keys, i, j);
}

/** The partition, by relative positions: `left` and `right` run unchecked in .NET, and a read past
 *  the span is its IndexOutOfRangeException. */
function pickPivotAndPartition<T>(keys: T[], lo: number, size: number, compare: (a: T, b: T) => number): number {
  const hi = size - 1;
  const middle = hi >> 1;
  swapIfGreater(keys, compare, lo, lo + middle);
  swapIfGreater(keys, compare, lo, lo + hi);
  swapIfGreater(keys, compare, lo + middle, lo + hi);
  const pivot = keys[lo + middle];
  swap(keys, lo + middle, lo + hi - 1);
  let left = 0;
  let right = hi - 1;
  while (left < right) {
    do {
      if (++left >= size) throw new OutOfRange();
    } while (compare(keys[lo + left], pivot) < 0);
    do {
      if (--right < 0) throw new OutOfRange();
    } while (compare(pivot, keys[lo + right]) < 0);
    if (left >= right) break;
    swap(keys, lo + left, lo + right);
  }
  if (left !== hi - 1) swap(keys, lo + left, lo + hi - 1);
  return left;
}

function heapSort<T>(keys: T[], lo: number, n: number, compare: (a: T, b: T) => number): void {
  for (let i = n >> 1; i >= 1; i--) downHeap(keys, lo, i, n, compare);
  for (let i = n; i > 1; i--) {
    swap(keys, lo, lo + i - 1);
    downHeap(keys, lo, 1, i - 1, compare);
  }
}

function downHeap<T>(keys: T[], lo: number, i: number, n: number, compare: (a: T, b: T) => number): void {
  const d = keys[lo + i - 1];
  while (i <= n >> 1) {
    let child = 2 * i;
    if (child < n && compare(keys[lo + child - 1], keys[lo + child]) < 0) child++;
    if (!(compare(d, keys[lo + child - 1]) < 0)) break;
    keys[lo + i - 1] = keys[lo + child - 1];
    i = child;
  }
  keys[lo + i - 1] = d;
}

function insertionSort<T>(keys: T[], lo: number, n: number, compare: (a: T, b: T) => number): void {
  for (let i = 0; i < n - 1; i++) {
    const t = keys[lo + i + 1];
    let j = i;
    while (j >= 0 && compare(t, keys[lo + j]) < 0) {
      keys[lo + j + 1] = keys[lo + j];
      j--;
    }
    keys[lo + j + 1] = t;
  }
}

// ---- GenericArraySortHelper<T>: the type's own CompareTo ---------------------------------------

/** `left.CompareTo(right)`: a null receiver is .NET's NullReferenceException, which the sort wraps. */
function compareTo<T>(compare: (a: T, b: T) => number, left: T, right: T): number {
  if (left == null) throw new TypeError('Object reference not set to an instance of an object.');
  return compare(left, right);
}

function comparableSort<T>(keys: T[], lo: number, size: number, depthLimit: number, compare: (a: T, b: T) => number): void {
  let partitionSize = size;
  while (partitionSize > 1) {
    if (partitionSize <= SIZE_THRESHOLD) {
      if (partitionSize === 2) {
        comparableSwapIfGreater(keys, compare, lo, lo + 1);
        return;
      }
      if (partitionSize === 3) {
        comparableSwapIfGreater(keys, compare, lo, lo + 1);
        comparableSwapIfGreater(keys, compare, lo, lo + 2);
        comparableSwapIfGreater(keys, compare, lo + 1, lo + 2);
        return;
      }
      comparableInsertionSort(keys, lo, partitionSize, compare);
      return;
    }
    if (depthLimit === 0) {
      comparableHeapSort(keys, lo, partitionSize, compare);
      return;
    }
    depthLimit--;
    const p = comparablePartition(keys, lo, partitionSize, compare);
    comparableSort(keys, lo + p + 1, partitionSize - (p + 1), depthLimit, compare);
    partitionSize = p;
  }
}

function comparableSwapIfGreater<T>(keys: T[], compare: (a: T, b: T) => number, i: number, j: number): void {
  if (keys[i] != null && compareTo(compare, keys[i], keys[j]) > 0) swap(keys, i, j);
}

function comparablePartition<T>(keys: T[], lo: number, size: number, compare: (a: T, b: T) => number): number {
  const zero = lo;
  const last = lo + size - 1;
  const middle = lo + ((size - 1) >> 1);
  comparableSwapIfGreater(keys, compare, zero, middle);
  comparableSwapIfGreater(keys, compare, zero, last);
  comparableSwapIfGreater(keys, compare, middle, last);
  const nextToLast = lo + size - 2;
  const pivot = keys[middle];
  swap(keys, middle, nextToLast);
  let left = zero;
  let right = nextToLast;
  while (left < right) {
    if (pivot == null) {
      while (left < nextToLast && keys[++left] == null);
      while (right > zero && keys[--right] != null);
    } else {
      while (left < nextToLast && compareTo(compare, pivot, keys[++left]) > 0);
      while (right > zero && compareTo(compare, pivot, keys[--right]) < 0);
    }
    if (left >= right) break;
    swap(keys, left, right);
  }
  if (left !== nextToLast) swap(keys, left, nextToLast);
  return left - zero;
}

function comparableHeapSort<T>(keys: T[], lo: number, n: number, compare: (a: T, b: T) => number): void {
  for (let i = n >> 1; i >= 1; i--) comparableDownHeap(keys, lo, i, n, compare);
  for (let i = n; i > 1; i--) {
    swap(keys, lo, lo + i - 1);
    comparableDownHeap(keys, lo, 1, i - 1, compare);
  }
}

function comparableDownHeap<T>(keys: T[], lo: number, i: number, n: number, compare: (a: T, b: T) => number): void {
  const d = keys[lo + i - 1];
  while (i <= n >> 1) {
    let child = 2 * i;
    if (child < n && (keys[lo + child - 1] == null || compareTo(compare, keys[lo + child - 1], keys[lo + child]) < 0)) child++;
    if (keys[lo + child - 1] == null || !(compareTo(compare, d, keys[lo + child - 1]) < 0)) break;
    keys[lo + i - 1] = keys[lo + child - 1];
    i = child;
  }
  keys[lo + i - 1] = d;
}

function comparableInsertionSort<T>(keys: T[], lo: number, n: number, compare: (a: T, b: T) => number): void {
  for (let i = 0; i < n - 1; i++) {
    const t = keys[lo + i + 1];
    let j = i;
    while (j >= 0 && (t == null || compareTo(compare, t, keys[lo + j]) < 0)) {
      keys[lo + j + 1] = keys[lo + j];
      j--;
    }
    keys[lo + j + 1] = t;
  }
}

/**
 * .NET's binary search over `keys[index, index + count)`: the midpoint `lo + ((hi - lo) >> 1)`, so
 * among equal elements the one .NET finds, and the complement of the insertion point for a value that
 * is not there. Whatever the comparison throws is wrapped as the sort wraps it.
 */
export function binarySearchIn<T>(keys: readonly T[], index: number, count: number, value: T, order: SortOrder<T>): number {
  const compare = comparisonOf(order);
  try {
    let lo = index;
    let hi = index + count - 1;
    while (lo <= hi) {
      const i = lo + ((hi - lo) >> 1);
      const c = compare(keys[i], value);
      if (c === 0) return i;
      if (c < 0) lo = i + 1;
      else hi = i - 1;
    }
    return ~lo;
  } catch (error) {
    throw compareFailed(error);
  }
}
