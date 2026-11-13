/**
 * WHAT EACH COMPONENT'S POSITION LOWERED TO, so a render its owner asked for lowers the components it
 * built and nothing else (E3b-3, #1455; docs/ELEMENT-TREE-PLAN.md 3.7).
 *
 * A component's lowering is the node its build returned, lowered; and then its PARENTS act on that
 * node: a flex places it (`align-self`), a flexible lets it grow, a presence names its motion, a
 * shortcut marks it, a node's own funnel writes its key. So a component lowered alone is not yet what
 * the page drew. The record keeps the root as the component's own lowering left it (`snapshot`), and
 * the root as the page drew it is the same object, `root`, which its parents went on to change. What
 * they did is the difference between the two, and it is replayed on the root a lowering alone makes
 * (`replay`), but only where it is a difference the replay can say exactly: the replay of the snapshot
 * must give back the page's root, attribute for attribute, or the record is not patched alone.
 *
 * A parent can also READ what its child lowered to, and decide its own element from it (whether the
 * child asks for all of an axis, its transition list, the shortcut chord already on it), or walk into
 * the child's subtree and number its rows. Then the child's lowering is not its own: `reachedInto`
 * marks the record, and a patch climbs to the nearest record above it that nothing reached into, the
 * page in the end, which lowers whole as before.
 *
 * Held weakly, by position and by root: a position that leaves the tree, or a root the page no longer
 * draws, takes its record with it.
 */

import type { HtmlNode } from '../core/types';
import type { Element as Position } from './runtime-exports';
import { isAtomicClass } from './style-atomizer';

/** The root of a lowering as the component's own lowering left it, before any parent acted on it. */
interface RootSnapshot {
  readonly key: string | number | undefined;
  readonly attributes: Readonly<Record<string, string | undefined>>;
  readonly events: Readonly<Record<string, unknown>>;
}

/** What a component's position lowered to in the last render that lowered it. */
export interface LoweredRecord {
  /** The component's position. */
  readonly position: Position;
  /** The record of the nearest component above, in the same tree: what a patch climbs to. */
  readonly parent: LoweredRecord | null;
  /** The root it lowered to, as the page drew it, its parents' changes included. Null: nothing. */
  root: HtmlNode | null;
  /** The same root as the component's own lowering left it. */
  snapshot: RootSnapshot | null;
  /** An ancestor read what this lowered to, or walked into it: it cannot be lowered alone. */
  reachedInto: boolean;
  /** What the lowering needs to lower it again exactly as it did: its context and its ambients. */
  readonly inputs: unknown;
}

const byPosition = new WeakMap<object, LoweredRecord>();
/** How many component lowerings ran: the test seam a render's cost is counted by. */
let lowerings = 0;

/** Test seam: how many components have been lowered so far. */
export function componentLowerings(): number {
  return lowerings;
}

const byRoot = new WeakMap<object, LoweredRecord>();
let open: LoweredRecord | null = null;

/** The positions the owner built while `collectBuilds` runs, in the order they built. */
let built: Position[] | null = null;

/** Called for every position an owner builds (`element-engine.ts`): kept while a collection runs. */
export function noteBuilt(position: Position | null): void {
  if (built && position) built.push(position);
}

/** Runs `build` and answers what it returned and the positions it built. */
export function collectBuilds<T>(build: () => T): { result: T; positions: Position[] } {
  const outer = built;
  const positions: Position[] = [];
  built = positions;
  try {
    return { result: build(), positions };
  } finally {
    built = outer;
  }
}

/** The component lowering in flight, innermost first: what declares a shortcut now. */
export function openRecord(): LoweredRecord | null {
  return open;
}

/**
 * The position of the one of `records` that `record` is, or stands below: what declared a shortcut
 * whose binding names `record`. Matched by POSITION, since a component lowered again records itself
 * anew there: a binding declared before names the old record, one declared now names the new one,
 * and both belong to the same component. Undefined when `record` stands under none of them.
 */
export function declarerAmong(records: readonly LoweredRecord[], record: unknown): Position | undefined {
  for (let at = record as LoweredRecord | null | undefined; at; at = at.parent) {
    const position = at.position;
    if (records.some((candidate) => candidate.position === position)) return position;
  }
  return undefined;
}

/** The record of `position`, from the last render that lowered it, or null. */
export function recordOf(position: object): LoweredRecord | null {
  return byPosition.get(position) ?? null;
}

/**
 * Lowers a component at `position` through `lower`, and keeps what it lowered to. Every component
 * lowered inside it records itself with this one as its parent.
 */
export function lowerRecorded(
  position: Position,
  inputs: unknown,
  lower: () => HtmlNode | null,
  parent: LoweredRecord | null = open,
): HtmlNode | null {
  lowerings++;
  const record: LoweredRecord = { position, parent, root: null, snapshot: null, reachedInto: false, inputs };
  const outer = open;
  open = record;
  let lowered: HtmlNode | null;
  try {
    lowered = lower();
  } finally {
    open = outer;
  }
  keep(record, lowered);
  return lowered;
}

/** Keeps `lowered` as what `record`'s position drew, as its own lowering left it. */
export function keep(record: LoweredRecord, lowered: HtmlNode | null): void {
  record.root = lowered;
  record.snapshot = lowered ? snapshotOf(lowered) : null;
  record.reachedInto = false;
  byPosition.set(record.position, record);
  if (lowered) byRoot.set(lowered, record);
}

/**
 * An ancestor reads what `node` lowered to, or walks into it: when it is a component's root, that
 * component cannot be lowered alone. Called by every reader of a lowered child, and on every node a
 * walk over a lowered subtree visits.
 */
export function reachedInto(node: HtmlNode | null | undefined): void {
  if (!node) return;
  const record = byRoot.get(node);
  if (record) record.reachedInto = true;
}

/**
 * The record to lower again for a component that built: its own, unless an ancestor reached into it,
 * or it has none (a component a container placed itself, read through for what it builds); then the
 * nearest one above that nothing reached into. Null when there is none: the page lowers whole.
 */
export function patchableAt(position: Position): LoweredRecord | null {
  let record: LoweredRecord | null = null;
  for (let at: Position | null = position; at && !record; at = at.parent) record = recordOf(at);
  while (record && (record.reachedInto || !record.root || !record.snapshot)) record = record.parent;
  return record;
}

/**
 * What the record's parents did to its root, done again to `next`, the root a lowering alone made.
 * Null when what they did cannot be said exactly: the replay of the snapshot does not give back the
 * root the page drew, so the record must not be patched alone.
 */
export function replay(
  record: Pick<LoweredRecord, 'root' | 'snapshot'>,
  next: HtmlNode | null,
): HtmlNode | null | undefined {
  const drawn = record.root;
  const before = record.snapshot;
  if (!drawn || !before || !next) return next === null && drawn === null ? null : undefined;
  const applied = (target: RootSnapshot): RootSnapshot | undefined => apply(before, current(drawn), target);
  // The replay must give back what the page drew from what the component lowered to; anything it
  // cannot say exactly is refused rather than guessed.
  const check = applied(before);
  if (!check || !same(check, current(drawn))) return undefined;
  const result = applied(snapshotOf(next));
  if (!result) return undefined;
  if (result.key === undefined) delete next.key;
  else next.key = result.key as string;
  next.attributes = { ...result.attributes } as Record<string, string>;
  next.events = { ...result.events } as HtmlNode['events'];
  return next;
}

function snapshotOf(node: HtmlNode): RootSnapshot {
  return { key: node.key ?? undefined, attributes: { ...node.attributes }, events: { ...node.events } };
}

function current(node: HtmlNode): RootSnapshot {
  return snapshotOf(node);
}

/**
 * `target` with the difference from `before` to `after` laid on it: a key set, an attribute written
 * or taken away, an event handled, and classes as the lowering adds them, a semantic class in front
 * and an atomic one into the sorted set behind.
 */
function apply(before: RootSnapshot, after: RootSnapshot, target: RootSnapshot): RootSnapshot | undefined {
  const attributes: Record<string, string | undefined> = { ...target.attributes };
  for (const name of new Set([...Object.keys(before.attributes), ...Object.keys(after.attributes)])) {
    if (name === 'class') continue;
    if (before.attributes[name] === after.attributes[name]) continue;
    if (after.attributes[name] === undefined) delete attributes[name];
    else attributes[name] = after.attributes[name];
  }
  const classes = mergeClasses(before.attributes['class'], after.attributes['class'], target.attributes['class']);
  if (classes === undefined) return undefined;
  if (classes === '') delete attributes['class'];
  else attributes['class'] = classes;

  const events: Record<string, unknown> = { ...target.events };
  for (const name of new Set([...Object.keys(before.events), ...Object.keys(after.events)])) {
    if (before.events[name] === after.events[name]) continue;
    if (after.events[name] === undefined) delete events[name];
    else events[name] = after.events[name];
  }
  const key = before.key === after.key ? target.key : after.key;
  return { key, attributes, events };
}

/**
 * The classes `after` has over `before`, put on `target` the way the lowering puts them. Two ways
 * are known, and the one that gives back `after` from `before` is the one used:
 * - semantic classes in front and atomic ones as a sorted set behind (`prependClass`,
 *   `mergeAtomicDeclaration`, `replaceAtomicDeclaration`): the semantic classes a parent added stay
 *   where they stood around the child's own, and the atomic set takes the parent's and loses what
 *   the parent took away;
 * - the child's classes as one run, with the parent's before and after it (a class appended to the
 *   string, as a bookmark's room is).
 * Neither giving back `after` means the classes cannot be said, and the record is not patched alone.
 */
function mergeClasses(before: string | undefined, after: string | undefined, target: string | undefined): string | undefined {
  const own = tokens(before);
  const drawn = tokens(after);
  const next = tokens(target);
  for (const model of [bySet, byRun]) {
    const back = model(own, drawn, own);
    if (back !== undefined && back.join(' ') === drawn.join(' ')) return model(own, drawn, next)?.join(' ');
  }
  return undefined;
}

/** Semantic classes in order, then the atomic ones sorted: what `mergeAtomicDeclaration` writes. */
function bySet(own: string[], drawn: string[], next: string[]): string[] | undefined {
  const removed = new Set(own.filter((c) => !drawn.includes(c)));
  const added = drawn.filter((c) => !own.includes(c));
  const firstOwn = drawn.findIndex((c) => own.includes(c) && !isAtomicClass(c));
  const lead = added.filter((c) => !isAtomicClass(c) && (firstOwn < 0 || drawn.indexOf(c) < firstOwn));
  const tail = added.filter((c) => !isAtomicClass(c) && !lead.includes(c));
  const kept = next.filter((c) => !removed.has(c));
  const semantic = [...lead, ...kept.filter((c) => !isAtomicClass(c)), ...tail];
  const atomic = [...new Set([...kept.filter(isAtomicClass), ...added.filter(isAtomicClass)])].sort();
  return [...semantic, ...atomic];
}

/** The child's classes as one run inside the parent's. */
function byRun(own: string[], drawn: string[], next: string[]): string[] | undefined {
  if (own.length === 0) return undefined;
  const first = drawn.indexOf(own[0]);
  if (first < 0 || drawn.slice(first, first + own.length).join(' ') !== own.join(' ')) return undefined;
  return [...drawn.slice(0, first), ...next, ...drawn.slice(first + own.length)];
}

function tokens(value: string | undefined): string[] {
  return (value ?? '').split(' ').filter(Boolean);
}

function same(a: RootSnapshot, b: RootSnapshot): boolean {
  if (a.key !== b.key) return false;
  return sameBag(a.attributes, b.attributes) && sameBag(a.events, b.events);
}

function sameBag(a: Readonly<Record<string, unknown>>, b: Readonly<Record<string, unknown>>): boolean {
  const keys = (bag: Readonly<Record<string, unknown>>) => Object.keys(bag).filter((name) => bag[name] !== undefined);
  const left = keys(a);
  if (left.length !== keys(b).length) return false;
  return left.every((name) => a[name] === b[name]);
}
