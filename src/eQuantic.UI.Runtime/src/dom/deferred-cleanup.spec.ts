/**
 * A timer that cleans up after a gesture carries what it cleans up, and never reads the global it
 * would find when it fires.
 *
 * Twice a `setTimeout(() => document.removeEventListener(…), 50)` outlived the environment it was
 * set in: a suite's teardown removed `document` while the timer was pending, the callback threw
 * where nothing could catch it, and vitest failed a run in which every file had passed. draggable.ts
 * was fixed, and drag-dismiss.ts, which had the same shape, failed CI again weeks later. So this
 * reads the runtime's own source and refuses the shape wherever it is written.
 */
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';

/** Every TypeScript source under `dir`, specs and declarations left out. */
function sources(dir: string): string[] {
  const found: string[] = [];
  for (const name of readdirSync(dir)) {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) found.push(...sources(path));
    else if (name.endsWith('.ts') && !name.endsWith('.spec.ts') && !name.endsWith('.d.ts')) found.push(path);
  }
  return found;
}

describe('deferred cleanup', () => {
  it('never reads document or window when a timer fires', () => {
    // A callback whose first word is the global, on the line of the timer or the next one.
    const shape = /setTimeout\(\s*\(\)\s*=>\s*\{?\s*(document|window)\./g;
    const offences: string[] = [];
    for (const file of sources('src')) {
      const text = readFileSync(file, 'utf8');
      for (const match of text.matchAll(shape)) {
        const line = text.slice(0, match.index).split('\n').length;
        offences.push(`${file}:${line}`);
      }
    }
    expect(offences, 'hold the document in a local before the timer, as draggable.ts does').toEqual([]);
  });
});
