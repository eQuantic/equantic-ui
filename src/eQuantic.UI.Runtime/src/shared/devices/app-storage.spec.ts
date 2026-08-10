import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { WebAppStorage } from './app-storage';

/**
 * `localStorage` is the DOM API most likely to THROW rather than fail quietly — Safari's private
 * mode has historically thrown on write, a browser at its quota throws, and reading it at all
 * throws when the origin has storage blocked (a third-party frame, "block all cookies"). A
 * preference failing to save must not take the page down with it.
 */
describe('WebAppStorage', () => {
  let storage: WebAppStorage;

  beforeEach(() => {
    storage = new WebAppStorage();
    localStorage.clear();
  });

  afterEach(() => vi.restoreAllMocks());

  it('round-trips a value', () => {
    storage.set('theme', 'dark');
    expect(storage.get('theme')).toBe('dark');
  });

  it('answers null for a key never set', () => {
    expect(storage.get('nothing-here')).toBeNull();
  });

  it('forgets', () => {
    storage.set('theme', 'dark');
    storage.remove('theme');
    expect(storage.get('theme')).toBeNull();
  });

  it('forgetting what was never there is not an error', () => {
    expect(() => storage.remove('nothing-here')).not.toThrow();
  });

  it('a blocked read answers null instead of throwing', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new DOMException('The operation is insecure.', 'SecurityError');
    });

    // Indistinguishable, to a caller, from an unset key — which they already handle.
    expect(storage.get('theme')).toBeNull();
  });

  it('a write that cannot happen does not take the page down', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('Quota exceeded.', 'QuotaExceededError');
    });

    expect(() => storage.set('theme', 'dark')).not.toThrow();
  });
});
