/**
 * The LINQ sequences that come from nothing: `Enumerable.Range` and `Enumerable.Repeat`, as .NET
 * answers them. A sequence is an array here, as every other LINQ translation takes it.
 *
 * Both arrive with their arguments evaluated once, where C# evaluates them. The transpiler wrote each
 * as `Array.from` with a callback around the argument's C#, so `Range(Start(), 3)` called `Start`
 * three times and answered `1,3,5` for its `1,2,3`, `Repeat(new List<int>(), 3)` made three lists
 * where .NET repeats one, and an `await` in either argument could not parse.
 */
import { exception } from './exceptions';

const COUNT = "Specified argument was out of the range of valid values. (Parameter 'count')";

/** `Enumerable.Range(start, count)`: refused where the count is negative or the last value would
 * pass `int.MaxValue`, as .NET refuses it. */
export function range(start: number, count: number): number[] {
  if (count < 0 || start + count - 1 > 2_147_483_647) {
    throw exception('System.ArgumentOutOfRangeException', COUNT);
  }
  const values = new Array<number>(count);
  for (let i = 0; i < count; i++) values[i] = start + i;
  return values;
}

/** `Enumerable.Repeat(element, count)`: the one element, `count` times. */
export function repeat<T>(element: T, count: number): T[] {
  if (count < 0) throw exception('System.ArgumentOutOfRangeException', COUNT);
  return new Array<T>(count).fill(element);
}
