/**
 * The web frame clock: one requestAnimationFrame loop shared by every subscriber, running only
 * while someone is subscribed — a page with no per-frame motion costs nothing, which is the same
 * bargain the native host makes.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { WebFrameTicker } from './frame-ticker';

describe('WebFrameTicker (IFrameTicker realization)', () => {
  let frames: Array<(now: number) => void>;
  let cancelled: number[];

  beforeEach(() => {
    frames = [];
    cancelled = [];
    vi.stubGlobal('requestAnimationFrame', (callback: (now: number) => void) => {
      frames.push(callback);
      return frames.length;
    });
    vi.stubGlobal('cancelAnimationFrame', (handle: number) => cancelled.push(handle));
    vi.stubGlobal('performance', { now: () => 0 });
  });

  afterEach(() => vi.unstubAllGlobals());

  /** Runs the frame the loop is currently waiting on, at `now`. */
  const advance = (now: number): void => {
    const next = frames.shift();
    if (next) next(now);
  };

  it('delivers the frame time and the delta since the subscriber last saw one', () => {
    const ticker = new WebFrameTicker();
    const ticks: Array<{ timeMs: number; deltaMs: number }> = [];
    ticker.onFrame((tick) => ticks.push(tick));

    advance(16);
    advance(33);

    expect(ticks).toEqual([
      { timeMs: 16, deltaMs: 0 },
      { timeMs: 33, deltaMs: 17 },
    ]);
  });

  it('runs ONE loop for every subscriber', () => {
    const ticker = new WebFrameTicker();
    let a = 0;
    let b = 0;
    ticker.onFrame(() => a++);
    ticker.onFrame(() => b++);

    expect(frames.length).toBe(1);
    advance(16);
    expect([a, b]).toEqual([1, 1]);
  });

  it('stops the loop when the last subscription is disposed', () => {
    const ticker = new WebFrameTicker();
    const first = ticker.onFrame(() => {});
    const second = ticker.onFrame(() => {});

    advance(16);
    first.dispose();
    expect(cancelled).toHaveLength(0);

    second.dispose();
    expect(cancelled).toHaveLength(1);
  });

  it('lets a callback dispose itself without disturbing the frame being delivered', () => {
    const ticker = new WebFrameTicker();
    let mine = 0;
    let other = 0;
    const self = ticker.onFrame(() => {
      mine++;
      self.dispose();
    });
    ticker.onFrame(() => other++);

    advance(16);
    advance(33);

    expect(mine).toBe(1);
    expect(other).toBe(2);
  });
});
