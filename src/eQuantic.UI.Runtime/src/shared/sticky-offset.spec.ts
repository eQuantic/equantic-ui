/**
 * The room a bookmark keeps above itself is MEASURED from the chrome that would cover it, because
 * the height of a content-sized bar is not knowable before layout — and because one app here has a
 * 60dp nav on one page and a 56dp topbar on another, neither of which states a number.
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { publishAnchorOffset } from './sticky-offset';

describe('the anchor offset comes from the sticky, not from a constant', () => {
  const sticky = (top: number, height: number): HTMLElement => {
    const element = document.createElement('div');
    element.setAttribute('data-eq-sticky', '1');
    element.getBoundingClientRect = () => ({
      top, height, bottom: top + height, left: 0, right: 0, width: 0, x: 0, y: top, toJSON: () => ({}),
    });
    document.body.appendChild(element);
    return element;
  };

  beforeEach(() => {
    document.body.innerHTML = '';
    document.documentElement.style.removeProperty('--eq-anchor-offset');
  });

  afterEach(() => document.documentElement.style.removeProperty('--eq-anchor-offset'));

  const offset = (): string => document.documentElement.style.getPropertyValue('--eq-anchor-offset');

  it('publishes the height of the chrome pinned at the top', () => {
    sticky(0, 60);
    publishAnchorOffset();
    expect(offset()).toBe('60px');
  });

  it('takes the TALLEST when more than one is pinned', () => {
    sticky(0, 56);
    sticky(0, 60);
    publishAnchorOffset();
    expect(offset()).toBe('60px');
  });

  it('ignores a sticky that has not pinned yet', () => {
    // Still down the page: it covers nothing a bookmark would land under.
    sticky(300, 60);
    publishAnchorOffset();
    expect(offset()).toBe('0px');
  });

  it('is zero when the page has no chrome at all', () => {
    publishAnchorOffset();
    expect(offset()).toBe('0px');
  });
});

describe('the offset reaches a real run', () => {
  it('is published when a reconciler pass ends', async () => {
    // The first version of this shipped with the publisher WRITTEN AND NEVER CALLED: the edit that
    // wired it used an anchor that only existed on another branch and silently did nothing. A
    // module nobody calls is a feature nobody gets, so the call is asserted through the real seam.
    document.body.innerHTML = '';
    document.documentElement.style.removeProperty('--eq-anchor-offset');

    const bar = document.createElement('div');
    bar.setAttribute('data-eq-sticky', '1');
    bar.getBoundingClientRect = () => ({
      top: 0, height: 56, bottom: 56, left: 0, right: 0, width: 0, x: 0, y: 0, toJSON: () => ({}),
    });
    document.body.appendChild(bar);

    const { ComponentInstanceStore } = await import('./instance-store');
    const { enterPass, exitPass } = await import('./instance-store');
    enterPass(new ComponentInstanceStore(), null);
    exitPass();

    expect(document.documentElement.style.getPropertyValue('--eq-anchor-offset')).toBe('56px');
  });
});
