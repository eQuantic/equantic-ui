import { describe, expect, it, vi, afterEach } from 'vitest';
// The lowercase factory is what a transpiled call site uses: `$eq.time.timeSpan.fromSeconds(…)`.
import { timeSpan } from '../../utils/datetime';
import { WebClock } from './clock';

/**
 * The browser's half of `IClock`. The interval arrives as the transpiled TimeSpan, so the numbers
 * here are the ones a C# component wrote: `TimeSpan.FromSeconds(1.7)` and nothing else.
 */
describe('WebClock', () => {
  afterEach(() => vi.useRealTimers());

  it('ticks every interval, starting one interval in', () => {
    vi.useFakeTimers();
    const clock = new WebClock();
    let ticks = 0;

    clock.every(timeSpan.fromSeconds(1.7), () => ticks++);

    expect(ticks).toBe(0);
    vi.advanceTimersByTime(1699);
    expect(ticks).toBe(0);
    vi.advanceTimersByTime(1);
    expect(ticks).toBe(1);
    vi.advanceTimersByTime(1700 * 3);
    expect(ticks).toBe(4);
  });

  it('stops when disposed, and disposing twice is fine', () => {
    vi.useFakeTimers();
    const clock = new WebClock();
    let ticks = 0;

    const subscription = clock.every(timeSpan.fromMilliseconds(100), () => ticks++);
    vi.advanceTimersByTime(250);
    expect(ticks).toBe(2);

    subscription.dispose();
    subscription.dispose();
    vi.advanceTimersByTime(1000);
    expect(ticks).toBe(2);
  });

  /** A zero interval is a spin loop, and the framework should not amplify a component's bug. */
  it('clamps a zero interval instead of spinning', () => {
    vi.useFakeTimers();
    const clock = new WebClock();
    let ticks = 0;

    clock.every(timeSpan.fromMilliseconds(0), () => ticks++);
    vi.advanceTimersByTime(1);

    expect(ticks).toBe(1);
  });
});
