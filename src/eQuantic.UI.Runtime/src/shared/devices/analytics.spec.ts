/**
 * The web realization of `IAnalytics`: one push into the tag manager's dataLayer — GATED on an
 * installer having declared itself. Without `__EQ_ANALYTICS__`, track() is a silent no-op:
 * filling an array nobody drains is not analytics, it is a leak with a respectable name.
 */

import { afterEach, describe, expect, it } from 'vitest';
import { WebAnalytics } from './analytics';

interface AnalyticsWindow {
  __EQ_ANALYTICS__?: { dataLayer?: string };
  dataLayer?: unknown[];
  eqData?: unknown[];
}

const w = window as unknown as AnalyticsWindow;

describe('WebAnalytics (IAnalytics realization)', () => {
  afterEach(() => {
    delete w.__EQ_ANALYTICS__;
    delete w.dataLayer;
    delete w.eqData;
  });

  it('without an installer, tracking is a silent no-op', () => {
    new WebAnalytics().track('sign_up');

    expect(w.dataLayer).toBeUndefined();
  });

  it('with an installer, the event lands in the dataLayer — created if the container has not', () => {
    w.__EQ_ANALYTICS__ = {};

    new WebAnalytics().track('sign_up');

    // The dataLayer ARRAY is the tag manager's own queue: pushes made before the container
    // loads are replayed by the container itself — which is why this file has no buffer.
    expect(w.dataLayer).toEqual([{ event: 'sign_up' }]);
  });

  it('data flattens into the entry, whether it crossed as a Map or an object', () => {
    w.__EQ_ANALYTICS__ = {};
    const analytics = new WebAnalytics();

    analytics.track('purchase', new Map<string, unknown>([['value', 42]]));
    analytics.track('refund', { value: 7 });

    expect(w.dataLayer).toEqual([
      { event: 'purchase', value: 42 },
      { event: 'refund', value: 7 },
    ]);
  });

  it('a renamed dataLayer is honoured', () => {
    w.__EQ_ANALYTICS__ = { dataLayer: 'eqData' };

    new WebAnalytics().track('sign_up');

    expect(w.eqData).toEqual([{ event: 'sign_up' }]);
    expect(w.dataLayer).toBeUndefined();
  });
});
