/**
 * A fragment is USER INPUT, so the decode has to survive whatever arrives. `#%` is a malformed
 * percent-escape and `decodeURIComponent` throws on it — from the router that would reject a
 * navigation mid-flight, and from the anchor-offset pass it would break the microtask every render
 * schedules. Both halves of the bookmark story call this one function so the guard exists once.
 */
import { afterEach, describe, expect, it } from 'vitest';
import { bookmarkTarget } from './bookmark-target';

describe('the element a fragment names', () => {
  afterEach(() => {
    document.body.innerHTML = '';
  });

  it('finds the element the fragment names', () => {
    const el = document.createElement('div');
    el.id = 'rights';
    document.body.appendChild(el);
    expect(bookmarkTarget('#rights', document)).toBe(el);
  });

  it('decodes a percent-escaped name, which is how a browser sends a non-ASCII id', () => {
    const el = document.createElement('div');
    el.id = 'direitos';
    document.body.appendChild(el);
    expect(bookmarkTarget('#direi%74os', document)).toBe(el);
  });

  it('answers nothing for a malformed escape instead of throwing', () => {
    expect(() => bookmarkTarget('#%', document)).not.toThrow();
    expect(bookmarkTarget('#%', document)).toBeNull();
    expect(bookmarkTarget('#%E0%A4%A', document)).toBeNull();
  });

  it('answers nothing for an empty or bare fragment, and for a name nothing carries', () => {
    expect(bookmarkTarget('', document)).toBeNull();
    expect(bookmarkTarget('#', document)).toBeNull();
    expect(bookmarkTarget('#absent', document)).toBeNull();
  });
});
