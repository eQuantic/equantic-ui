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

import { Dictionary } from '../../utils/dictionary';

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
   * The C# `Track(eventName, data)` twin. `data` arrives as the runtime's `Dictionary` from
   * transpiled C#, or as a Map or a plain object from hand-written code, and flattens into the event
   * entry, the dataLayer's own shape.
   */
  track(
    eventName: string,
    data?: Dictionary<string, unknown> | Map<string, unknown> | Record<string, unknown> | null,
  ): void {
    if (typeof window === 'undefined') return;
    const config = window.__EQ_ANALYTICS__;
    if (!config) return;

    const name = config.dataLayer || 'dataLayer';
    const w = window as unknown as Record<string, unknown[]>;
    w[name] = w[name] || [];

    const entry: Record<string, unknown> = { event: eventName };
    // Each field DEFINED, not assigned: a key the data holds may be "__proto__", which an assignment
    // (and Object.assign) sends to the prototype's setter, dropping the field.
    const put = (key: string, value: unknown) =>
      Object.defineProperty(entry, key, { value, writable: true, enumerable: true, configurable: true });
    if (data instanceof Map || data instanceof Dictionary) {
      for (const [key, value] of data) put(key, value);
    } else if (data) {
      for (const key of Object.keys(data)) put(key, data[key]);
    }
    w[name].push(entry);
  }
}
