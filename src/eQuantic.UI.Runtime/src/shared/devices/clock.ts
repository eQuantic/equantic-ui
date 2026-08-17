import type { TimeSpan } from '../../utils/datetime';

/**
 * The browser's realization of the C# `IClock` — `setInterval`, and nothing more.
 *
 * The interval arrives as the transpiled `TimeSpan` (tick-precise), so the milliseconds come from it
 * rather than from the call site: a component says `TimeSpan.FromSeconds(1.7)` and this is where that
 * becomes a number the platform understands.
 *
 * Missed ticks are the platform's own answer here: a background tab is throttled and coalesced by
 * the browser, which is exactly the "one tick, not six hundred" the contract promises. No catch-up
 * loop of ours belongs on top of that.
 */
export class WebClock {
  every(interval: TimeSpan, onTick: () => void): { dispose(): void } {
    // A zero or negative interval would be a spin: the browser clamps it to its own minimum, and a
    // component asking for it has a bug the framework should not amplify.
    const ms = Math.max(1, Math.round(interval.totalMilliseconds));
    let handle: ReturnType<typeof setInterval> | null = setInterval(onTick, ms);
    return {
      dispose(): void {
        // Disposing twice is fine, says the contract, and it is the normal case: a component whose
        // OnUnmount already ran can be disposed again by whatever held the handle.
        if (handle === null) return;
        clearInterval(handle);
        handle = null;
      },
    };
  }
}
