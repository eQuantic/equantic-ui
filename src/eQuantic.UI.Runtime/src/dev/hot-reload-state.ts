import { StatefulComponent, StatelessComponent } from '../core/component';
import { Component } from '../core/types';
import { adoptMember } from '../utils/adopt-member';
import { hydrateValue } from '../utils/hydrate-value';

/**
 * What a hot reload carries across for a page: the fields its C# declares. A write-once page keeps
 * them on itself, as the class fields its twin declares (`_count = 0;`), and the reload read a
 * `_state` bag such a page does not have, so it carried nothing and every counter went back to its
 * initializer (#664).
 *
 * The runtime's own fields live on the same object, set by the base classes' constructors before the
 * page's: the nearest base class of the runtime, built bare, says which they are, with no list to keep
 * in step with it.
 */
const runtimeBases: ReadonlyArray<abstract new (...args: never[]) => object> = [
  StatefulComponent,
  StatelessComponent,
  Component,
];

function runtimeKeys(page: object): Set<string> {
  for (let prototype = Object.getPrototypeOf(page); prototype; prototype = Object.getPrototypeOf(prototype)) {
    const base = runtimeBases.find((candidate) => candidate.prototype === prototype);
    if (base) return new Set(Object.keys(Reflect.construct(base, [])));
  }
  return new Set();
}

/**
 * The page's fields, each as JSON can carry it across the reload. One that cannot cross (a cycle, a
 * bigint inside a plain object) is left behind alone, and the others still cross.
 */
export function capturePageState(page: object): Record<string, unknown> {
  const skip = runtimeKeys(page);
  const state: Record<string, unknown> = {};
  for (const key of Object.keys(page)) {
    if (skip.has(key)) continue;
    const value = (page as Record<string, unknown>)[key];
    if (typeof value === 'function') continue;
    try {
      JSON.stringify(value);
    } catch {
      continue;
    }
    state[key] = value;
  }
  return state;
}

/**
 * Hands the reloaded page the fields it held, each rebuilt in the shape its own initializer gives it
 * (`hydrateValue`), before it builds. Only a field the new page declares too: one the edit removed or
 * renamed is left behind, and one it added keeps its initializer.
 */
export function restorePageState(page: object, saved: Record<string, unknown>): void {
  const skip = runtimeKeys(page);
  for (const key of Object.keys(saved)) {
    if (skip.has(key) || !Object.prototype.hasOwnProperty.call(page, key)) continue;
    adoptMember(page, key, hydrateValue((page as Record<string, unknown>)[key], saved[key]));
  }
}
