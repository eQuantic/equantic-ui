/**
 * The web realization of C# `IFrameTicker`: `requestAnimationFrame`, shared by every subscriber.
 *
 * One rAF loop runs while anyone is subscribed and stops when the last subscription is disposed —
 * a page with no per-frame motion costs nothing, which is also what the native host does when its
 * ticker has no subscribers. The tick carries the same shape as the C# record: the frame clock in
 * milliseconds and the delta since the subscriber's previous frame (0 on its first).
 */
export interface FrameTick {
  timeMs: number;
  deltaMs: number;
}

type Subscriber = { onFrame: (tick: FrameTick) => void; lastMs: number | null };

export class WebFrameTicker {
  private readonly subscribers = new Set<Subscriber>();
  private handle: number | null = null;
  private readonly start = typeof performance !== 'undefined' ? performance.now() : Date.now();

  onFrame(onFrame: (tick: FrameTick) => void): { dispose(): void } {
    const subscriber: Subscriber = { onFrame, lastMs: null };
    this.subscribers.add(subscriber);
    this.ensureRunning();
    return {
      dispose: () => {
        this.subscribers.delete(subscriber);
        if (this.subscribers.size === 0 && this.handle !== null) {
          cancelAnimationFrame(this.handle);
          this.handle = null;
        }
      },
    };
  }

  private ensureRunning(): void {
    if (this.handle !== null || typeof requestAnimationFrame !== 'function') return;
    const step = (now: number): void => {
      this.handle = null;
      const timeMs = now - this.start;
      // Snapshot: a subscriber that disposes (or subscribes) inside its callback changes the set
      // for the NEXT frame, never the one being delivered.
      for (const subscriber of Array.from(this.subscribers)) {
        const deltaMs = subscriber.lastMs === null ? 0 : timeMs - subscriber.lastMs;
        subscriber.lastMs = timeMs;
        subscriber.onFrame({ timeMs, deltaMs });
      }
      if (this.subscribers.size > 0) this.handle = requestAnimationFrame(step);
    };
    this.handle = requestAnimationFrame(step);
  }
}
