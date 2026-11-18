/**
 * Which listeners hear a pointer (#1562): Flutter's `HitTestBehavior` rule, over what lies under the point. The C#
 * `ListenerHits` (Photon) writes the same rule, and both run the cases of `listener-hits.fixture.json`, so the two
 * writings cannot drift.
 *
 * Over the entries under the point, in paint order:
 * - a listener's child is hit when a solid inside it is under the point, or an `opaque` listener is;
 * - an entry returns true when it is a solid, or a listener that is `opaque` or has its child hit;
 * - a listener is reached when nothing painted above it, outside its own subtree, returns true;
 * - a listener hears when it is reached, and its child is hit or it is not `deferToChild`;
 * - the listeners that hear are told innermost and topmost first: the latest painted first.
 */
import type { HitTestBehaviorValue } from '../shared/enums.generated';

/** One entry under the point. */
export interface UnderEntry {
  /** Its place in paint order. */
  start: number;
  /** The place past its subtree: the entries between are inside it. */
  end: number;
  /** Whether it draws or answers. */
  solid: boolean;
  /** Its behaviour when it is a listener, or null. */
  behavior: HitTestBehaviorValue | null;
}

/** The listeners of `under` (in paint order) that hear, as indices into it, the latest painted first. */
export function listenerHits(under: readonly UnderEntry[]): number[] {
  const heard: number[] = [];
  for (let i = under.length - 1; i >= 0; i--) {
    const behavior = under[i].behavior;
    if (behavior === null) continue;
    const childHit = childIsHit(under, i);
    if (!reached(under, i)) continue;
    if (childHit || behavior !== 'deferToChild') heard.push(i);
  }
  return heard;
}

function inside(outer: UnderEntry, entry: UnderEntry): boolean {
  return entry.start > outer.start && entry.start < outer.end;
}

function childIsHit(under: readonly UnderEntry[], listener: number): boolean {
  for (const entry of under) {
    if (!inside(under[listener], entry)) continue;
    if (entry.solid || entry.behavior === 'opaque') return true;
  }
  return false;
}

function returnsTrue(under: readonly UnderEntry[], entry: number): boolean {
  return (
    under[entry].solid ||
    under[entry].behavior === 'opaque' ||
    (under[entry].behavior !== null && childIsHit(under, entry))
  );
}

function reached(under: readonly UnderEntry[], listener: number): boolean {
  for (let j = 0; j < under.length; j++) {
    if (under[j].start < under[listener].end) continue;
    if (returnsTrue(under, j)) return false;
  }
  return true;
}
