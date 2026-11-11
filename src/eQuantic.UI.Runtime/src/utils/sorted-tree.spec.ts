import { describe, expect, it } from 'vitest';
import {
  addIfNotPresent,
  breadthFirst,
  constructFromSorted,
  doRemove,
  extreme,
  inOrder,
  type Tree,
  type TreeNode,
} from './sorted-tree';

const compare = (a: number, b: number): number => a - b;
const empty = (): Tree<number> => ({ root: null, count: 0, version: 0 });

/** The black height of a subtree, refusing two reds in a row and paths of unequal black height. */
function blackHeight(n: TreeNode<number> | null): number {
  if (n === null) return 1;
  if (n.red && ((n.left !== null && n.left.red) || (n.right !== null && n.right.red))) throw new Error(`red ${n.item} has a red child`);
  const left = blackHeight(n.left);
  const right = blackHeight(n.right);
  if (left !== right) throw new Error(`unequal black heights under ${n.item}`);
  return left + (n.red ? 0 : 1);
}

function expectRedBlack(tree: Tree<number>): void {
  if (tree.root !== null) expect(tree.root.red).toBe(false);
  blackHeight(tree.root);
}

describe('the red-black tree of a sorted set (#766)', () => {
  it('walks the tree of 1..7 added in order breadth first, as .NET does', () => {
    const tree = empty();
    for (let i = 1; i <= 7; i++) addIfNotPresent(tree, compare, i);
    expect(breadthFirst(tree, compare)).toEqual([2, 1, 4, 3, 6, 5, 7]);
    expect(inOrder(tree, compare)).toEqual([1, 2, 3, 4, 5, 6, 7]);
  });

  it('stays a red-black tree, sorted and counted, through a long history of adds and removes', () => {
    const tree = empty();
    const mirror = new Set<number>();
    let x = 7;
    for (let step = 0; step < 4000; step++) {
      x = (x * 1103515245 + 12345) % 2147483648;
      const value = x % 300;
      if (x % 3 === 0) {
        expect(doRemove(tree, compare, value)).toBe(mirror.delete(value));
      } else {
        expect(addIfNotPresent(tree, compare, value)).toBe(!mirror.has(value));
        mirror.add(value);
      }
      expectRedBlack(tree);
      expect(tree.count).toBe(mirror.size);
    }
    expect(inOrder(tree, compare)).toEqual([...mirror].sort(compare));
  });

  it('builds a red-black tree from a sorted array of every size', () => {
    for (let size = 0; size <= 64; size++) {
      const items = Array.from({ length: size }, (_, i) => i);
      const tree: Tree<number> = { root: constructFromSorted(items, 0, size - 1, null), count: size, version: 0 };
      expectRedBlack(tree);
      expect(inOrder(tree, compare)).toEqual(items);
    }
  });

  it('finds the least and the greatest of a range, and walks only that range', () => {
    const tree = empty();
    for (let i = 1; i <= 15; i++) addIfNotPresent(tree, compare, i);
    const range = { lower: 4, upper: 11 };
    expect(extreme(tree, compare, 'min', range)).toBe(4);
    expect(extreme(tree, compare, 'max', range)).toBe(11);
    expect(extreme(tree, compare, 'min')).toBe(1);
    expect(extreme(empty(), compare, 'max')).toBeUndefined();
    expect([...breadthFirst(tree, compare, range)].sort(compare)).toEqual(inOrder(tree, compare, range));
  });
});
