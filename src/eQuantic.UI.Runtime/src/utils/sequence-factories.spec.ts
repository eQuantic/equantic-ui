import { describe, expect, it } from 'vitest';
import { typesOf } from './exceptions';
import { range, repeat } from './sequence-factories';

/** The .NET type a call throws, the most derived, or undefined where it throws nothing. */
function thrownType(run: () => unknown): string | undefined {
  try {
    run();
  } catch (error) {
    return typesOf(error)?.[0];
  }
  return undefined;
}

describe('Enumerable.Range and Enumerable.Repeat', () => {
  it('counts up from the start', () => {
    expect(range(1, 3)).toEqual([1, 2, 3]);
    expect(range(-2, 2)).toEqual([-2, -1]);
    expect(range(5, 0)).toEqual([]);
    expect(range(2_147_483_647, 1)).toEqual([2_147_483_647]);
  });

  it('refuses a negative count and a last value past int.MaxValue, as .NET does', () => {
    expect(thrownType(() => range(0, -1))).toBe('System.ArgumentOutOfRangeException');
    expect(thrownType(() => range(2_147_483_647, 2))).toBe('System.ArgumentOutOfRangeException');
  });

  it('repeats the one element it is handed', () => {
    const list: number[] = [];
    const repeated = repeat(list, 3);
    expect(repeated).toHaveLength(3);
    expect(repeated[0]).toBe(list);
    expect(repeated[2]).toBe(list);
    expect(repeat('a', 0)).toEqual([]);
  });

  it('refuses a negative count, as .NET does', () => {
    expect(thrownType(() => repeat(1, -1))).toBe('System.ArgumentOutOfRangeException');
  });
});
