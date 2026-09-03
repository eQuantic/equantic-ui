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
