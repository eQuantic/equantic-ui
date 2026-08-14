/**
 * The web realization of C# `IAnalytics` — one push into the tag manager's dataLayer.
 *
 * GATED on an installer: `UseGtm` (or any future collector) declares itself through
 * `window.__EQ_ANALYTICS__` before the runtime boots. Without it, track() is a silent no-op —
 * filling an array nobody will ever drain is not analytics, it is a leak with a respectable name.
 *
 * With it, the push goes into the dataLayer ARRAY, which is the tag manager's own queue: pushes
 * that land before the container script finishes loading are replayed by the container itself.
 * That is why this file needs no buffer, no readiness flag and no retry — the protocol already
 * has all three.
 */

/** What an installer (UseGtm) declares before boot. */
interface AnalyticsConfig {
  /** The dataLayer global's NAME — 'dataLayer' unless the installer renamed it. */
  dataLayer?: string;
}

declare global {
  interface Window {
    __EQ_ANALYTICS__?: AnalyticsConfig;
  }
}

export class WebAnalytics {
  /**
   * The C# `Track(eventName, data)` twin. `data` arrives as whatever the transpiled Dictionary
   * became — a Map or a plain object — and flattens into the event entry, the dataLayer's own
   * shape.
   */
  track(eventName: string, data?: Map<string, unknown> | Record<string, unknown> | null): void {
    if (typeof window === 'undefined') return;
    const config = window.__EQ_ANALYTICS__;
    if (!config) return;

    const name = config.dataLayer || 'dataLayer';
    const w = window as unknown as Record<string, unknown[]>;
    w[name] = w[name] || [];

    const entry: Record<string, unknown> = { event: eventName };
    if (data instanceof Map) {
      for (const [key, value] of data) entry[key] = value;
    } else if (data) {
      Object.assign(entry, data);
    }
    w[name].push(entry);
  }
}
