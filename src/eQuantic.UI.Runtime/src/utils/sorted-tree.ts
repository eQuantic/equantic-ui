/**
 * The red-black tree .NET's `SortedSet<T>` holds its elements in, kept node for node (#766).
 *
 * Its shape is the one thing a sorted set shows of it besides the order: `RemoveWhere` asks its predicate
 * breadth first, so the order the predicate is asked in is the tree's, which its history decides. A set
 * built by `Add` splits its 4-nodes on the way down; a `Remove` turns its 2-nodes on the way down, found
 * or not; a set copied from a sorted set of its comparer clones that set's tree; and a set built from any
 * other sequence, or merged with another set, is built anew from a sorted array, red at the bottom. Each
 * function here is the method of `System.Collections.Generic.SortedSet<T>` it is named after, in its
 * steps and its order, so the tree is the one .NET holds after the same calls.
 */

/** One node: its element, its children, and its color, red or black. */
export interface TreeNode<T> {
  item: T;
  left: TreeNode<T> | null;
  right: TreeNode<T> | null;
  red: boolean;
}

/** What a sorted set shares with every view taken of it: its root, its count, and the version every
 *  change bumps (#865). */
export interface Tree<T> {
  root: TreeNode<T> | null;
  count: number;
  version: number;
}

type Compare<T> = (a: T, b: T) => number;
type Rotation = 'left' | 'right' | 'leftRight' | 'rightLeft';

function node<T>(item: T, red: boolean): TreeNode<T> {
  return { item, left: null, right: null, red };
}

const isRed = <T>(n: TreeNode<T> | null): boolean => n !== null && n.red;
const isNullOrBlack = <T>(n: TreeNode<T> | null): boolean => n === null || !n.red;
const is2Node = <T>(n: TreeNode<T>): boolean => !n.red && isNullOrBlack(n.left) && isNullOrBlack(n.right);
const is4Node = <T>(n: TreeNode<T>): boolean => isRed(n.left) && isRed(n.right);

function split4Node<T>(n: TreeNode<T>): void {
  n.red = true;
  n.left!.red = false;
  n.right!.red = false;
}

function merge2Nodes<T>(n: TreeNode<T>): void {
  n.red = false;
  n.left!.red = true;
  n.right!.red = true;
}

function rotateLeft<T>(n: TreeNode<T>): TreeNode<T> {
  const child = n.right!;
  n.right = child.left;
  child.left = n;
  return child;
}

function rotateRight<T>(n: TreeNode<T>): TreeNode<T> {
  const child = n.left!;
  n.left = child.right;
  child.right = n;
  return child;
}

function rotateLeftRight<T>(n: TreeNode<T>): TreeNode<T> {
  const child = n.left!;
  const grandChild = child.right!;
  n.left = grandChild.right;
  grandChild.right = n;
  child.right = grandChild.left;
  grandChild.left = child;
  return grandChild;
}

function rotateRightLeft<T>(n: TreeNode<T>): TreeNode<T> {
  const child = n.right!;
  const grandChild = child.left!;
  n.right = grandChild.left;
  grandChild.left = n;
  child.left = grandChild.right;
  grandChild.right = child;
  return grandChild;
}

/** `Node.GetRotation`: how `parent` turns so `current`, a 2-node, takes a red from `sibling`. */
function rotationOf<T>(parent: TreeNode<T>, current: TreeNode<T>, sibling: TreeNode<T>): Rotation {
  const currentIsLeftChild = parent.left === current;
  return isRed(sibling.left) ? (currentIsLeftChild ? 'rightLeft' : 'right') : currentIsLeftChild ? 'left' : 'leftRight';
}

/** `Node.Rotate`. */
function rotate<T>(n: TreeNode<T>, rotation: Rotation): TreeNode<T> {
  switch (rotation) {
    case 'right':
      n.left!.left!.red = false;
      return rotateRight(n);
    case 'left':
      n.right!.right!.red = false;
      return rotateLeft(n);
    case 'rightLeft':
      return rotateRightLeft(n);
    case 'leftRight':
      return rotateLeftRight(n);
  }
}

function siblingOf<T>(parent: TreeNode<T>, n: TreeNode<T>): TreeNode<T> {
  return n === parent.left ? parent.right! : parent.left!;
}

function replaceChildOrRoot<T>(tree: Tree<T>, parent: TreeNode<T> | null, child: TreeNode<T>, newChild: TreeNode<T> | null): void {
  if (parent === null) tree.root = newChild;
  else if (parent.left === child) parent.left = newChild;
  else parent.right = newChild;
}

/** `InsertionBalance`: two reds in a row turned away; answers the parent the search goes on from. */
function insertionBalance<T>(
  tree: Tree<T>,
  current: TreeNode<T>,
  parent: TreeNode<T>,
  grandParent: TreeNode<T>,
  greatGrandParent: TreeNode<T> | null,
): TreeNode<T> | null {
  const parentIsOnRight = grandParent.right === parent;
  const currentIsOnRight = parent.right === current;
  let newChildOfGreatGrandParent: TreeNode<T>;
  let newParent: TreeNode<T> | null = parent;
  if (parentIsOnRight === currentIsOnRight) {
    newChildOfGreatGrandParent = currentIsOnRight ? rotateLeft(grandParent) : rotateRight(grandParent);
  } else {
    newChildOfGreatGrandParent = currentIsOnRight ? rotateLeftRight(grandParent) : rotateRightLeft(grandParent);
    newParent = greatGrandParent;
  }
  grandParent.red = true;
  newChildOfGreatGrandParent.red = false;
  replaceChildOrRoot(tree, greatGrandParent, grandParent, newChildOfGreatGrandParent);
  return newParent;
}

/** `AddIfNotPresent`: the version moves whether or not the item is added, since the way down may turn. */
export function addIfNotPresent<T>(tree: Tree<T>, compare: Compare<T>, item: T): boolean {
  if (tree.root === null) {
    tree.root = node(item, false);
    tree.count = 1;
    tree.version++;
    return true;
  }
  let current: TreeNode<T> | null = tree.root;
  let parent: TreeNode<T> | null = null;
  let grandParent: TreeNode<T> | null = null;
  let greatGrandParent: TreeNode<T> | null = null;
  tree.version++;
  let order = 0;
  while (current !== null) {
    order = compare(item, current.item);
    if (order === 0) {
      tree.root!.red = false;
      return false;
    }
    if (is4Node(current)) {
      split4Node(current);
      if (isRed(parent)) parent = insertionBalance(tree, current, parent!, grandParent!, greatGrandParent);
    }
    greatGrandParent = grandParent;
    grandParent = parent;
    parent = current;
    current = order < 0 ? current.left : current.right;
  }
  const added = node(item, true);
  if (order > 0) parent!.right = added;
  else parent!.left = added;
  if (parent!.red) insertionBalance(tree, added, parent!, grandParent!, greatGrandParent);
  tree.root!.red = false;
  tree.count++;
  return true;
}

/** `ReplaceNode`: the successor takes the matched node's place, links and color. */
function replaceNode<T>(
  tree: Tree<T>,
  match: TreeNode<T>,
  parentOfMatch: TreeNode<T> | null,
  successor: TreeNode<T> | null,
  parentOfSuccessor: TreeNode<T> | null,
): void {
  if (successor === match) {
    successor = match.left;
  } else {
    if (successor!.right !== null) successor!.right.red = false;
    if (parentOfSuccessor !== match) {
      parentOfSuccessor!.left = successor!.right;
      successor!.right = match.right;
    }
    successor!.left = match.left;
  }
  if (successor !== null) successor.red = match.red;
  replaceChildOrRoot(tree, parentOfMatch, match, successor);
}

/** `DoRemove`: every 2-node on the way down is turned into a 3- or 4-node, found or not. */
export function doRemove<T>(tree: Tree<T>, compare: Compare<T>, item: T): boolean {
  if (tree.root === null) return false;
  tree.version++;
  let current: TreeNode<T> | null = tree.root;
  let parent: TreeNode<T> | null = null;
  let grandParent: TreeNode<T> | null = null;
  let match: TreeNode<T> | null = null;
  let parentOfMatch: TreeNode<T> | null = null;
  let foundMatch = false;
  while (current !== null) {
    if (is2Node(current)) {
      if (parent === null) {
        current.red = true;
      } else {
        let sibling = siblingOf(parent, current);
        if (sibling.red) {
          if (parent.right === sibling) rotateLeft(parent);
          else rotateRight(parent);
          parent.red = true;
          sibling.red = false;
          replaceChildOrRoot(tree, grandParent, parent, sibling);
          grandParent = sibling;
          if (parent === match) parentOfMatch = sibling;
          sibling = siblingOf(parent, current);
        }
        if (is2Node(sibling)) {
          merge2Nodes(parent);
        } else {
          const newGrandParent = rotate(parent, rotationOf(parent, current, sibling));
          newGrandParent.red = parent.red;
          parent.red = false;
          current.red = true;
          replaceChildOrRoot(tree, grandParent, parent, newGrandParent);
          if (parent === match) parentOfMatch = newGrandParent;
        }
      }
    }
    const order: number = foundMatch ? -1 : compare(item, current.item);
    if (order === 0) {
      foundMatch = true;
      match = current;
      parentOfMatch = parent;
    }
    grandParent = parent;
    parent = current;
    current = order < 0 ? current.left : current.right;
  }
  if (match !== null) {
    replaceNode(tree, match, parentOfMatch, parent, grandParent);
    tree.count--;
  }
  if (tree.root !== null) tree.root.red = false;
  return foundMatch;
}

/** `ConstructRootFromSortedArray`: a tree of `items[start..end]`, which are sorted and distinct, black
 *  but for its deepest level's odd one out. */
export function constructFromSorted<T>(items: readonly T[], start: number, end: number, redNode: TreeNode<T> | null): TreeNode<T> | null {
  const size = end - start + 1;
  let root: TreeNode<T>;
  switch (size) {
    case 0:
      return null;
    case 1:
      root = node(items[start], false);
      if (redNode !== null) root.left = redNode;
      break;
    case 2:
      root = node(items[start], false);
      root.right = node(items[end], true);
      if (redNode !== null) root.left = redNode;
      break;
    case 3:
      root = node(items[start + 1], false);
      root.left = node(items[start], false);
      root.right = node(items[end], false);
      if (redNode !== null) root.left.left = redNode;
      break;
    default: {
      const midpoint = Math.trunc((start + end) / 2);
      root = node(items[midpoint], false);
      root.left = constructFromSorted(items, start, midpoint - 1, redNode);
      root.right =
        size % 2 === 0
          ? constructFromSorted(items, midpoint + 2, end, node(items[midpoint + 1], true))
          : constructFromSorted(items, midpoint + 1, end, null);
      break;
    }
  }
  return root;
}

/** `Node.DeepClone`: the same shape and colors, over the same elements. */
export function cloneTree<T>(root: TreeNode<T>): TreeNode<T> {
  const copy = (source: TreeNode<T>): TreeNode<T> => {
    const target = node(source.item, source.red);
    if (source.left !== null) target.left = copy(source.left);
    if (source.right !== null) target.right = copy(source.right);
    return target;
  };
  return copy(root);
}

/** `FindNode`: the node holding an element the comparer finds equal to `item`, or null. */
export function findNode<T>(tree: Tree<T>, compare: Compare<T>, item: T): TreeNode<T> | null {
  let current: TreeNode<T> | null = tree.root;
  while (current !== null) {
    const order: number = compare(item, current.item);
    if (order === 0) return current;
    current = order < 0 ? current.left : current.right;
  }
  return null;
}

/** `FindRange`: the highest node whose element lies from `lower` to `upper`, the root of a view's walk. */
export function findRange<T>(tree: Tree<T>, compare: Compare<T>, lower: T, upper: T): TreeNode<T> | null {
  let current: TreeNode<T> | null = tree.root;
  while (current !== null) {
    if (compare(lower, current.item) > 0) current = current.right;
    else if (compare(upper, current.item) < 0) current = current.left;
    else return current;
  }
  return null;
}

/**
 * `Min` and `Max`: the smallest or the largest element, from `lower` to `upper` where a range is given,
 * down one side of the tree as .NET walks it, or undefined when there is none.
 */
export function extreme<T>(
  tree: Tree<T>,
  compare: Compare<T>,
  side: 'min' | 'max',
  range?: { readonly lower: T; readonly upper: T },
): T | undefined {
  let current: TreeNode<T> | null = tree.root;
  let found: T | undefined;
  while (current !== null) {
    if (range !== undefined && compare(range.lower, current.item) > 0) current = current.right;
    else if (range !== undefined && compare(range.upper, current.item) < 0) current = current.left;
    else {
      found = current.item;
      current = side === 'min' ? current.left : current.right;
    }
  }
  return found;
}

/** The elements in order, from `lower` to `upper` where a range is given. */
export function inOrder<T>(tree: Tree<T>, compare: Compare<T>, range?: { readonly lower: T; readonly upper: T }): T[] {
  const items: T[] = [];
  const walk = (n: TreeNode<T> | null): void => {
    if (n === null) return;
    const aboveLower = range === undefined || compare(range.lower, n.item) <= 0;
    const belowUpper = range === undefined || compare(range.upper, n.item) >= 0;
    if (aboveLower) walk(n.left);
    if (aboveLower && belowUpper) items.push(n.item);
    if (belowUpper) walk(n.right);
  };
  walk(tree.root);
  return items;
}

/**
 * `BreadthFirstTreeWalk`: the elements level by level, each level from the left. A view's walk starts at
 * the root of its range, skips an element outside it, and goes down only a side that can hold one inside.
 */
export function breadthFirst<T>(tree: Tree<T>, compare: Compare<T>, range?: { readonly lower: T; readonly upper: T }): T[] {
  const items: T[] = [];
  const start = range === undefined ? tree.root : findRange(tree, compare, range.lower, range.upper);
  if (start === null) return items;
  const queue: TreeNode<T>[] = [start];
  for (let at = 0; at < queue.length; at++) {
    const current = queue[at];
    if (range === undefined || (compare(range.lower, current.item) <= 0 && compare(range.upper, current.item) >= 0)) {
      items.push(current.item);
    }
    if (current.left !== null && (range === undefined || compare(range.lower, current.item) < 0)) queue.push(current.left);
    if (current.right !== null && (range === undefined || compare(range.upper, current.item) > 0)) queue.push(current.right);
  }
  return items;
}
