/**
 * The web realization of `IConsent`: one cookie both sides read, one document event a collector
 * waits for. The cookie is the contract — the server reads the same name on the next request, and
 * the GTM installer's head script decides from it whether a tag manager is downloaded at all.
 */

import { afterEach, describe, expect, it } from 'vitest';
import { CONSENT_COOKIE, WebConsent } from './consent';

function clearCookie(): void {
  document.cookie = `${CONSENT_COOKIE}=; path=/; max-age=0`;
}

describe('WebConsent (IConsent realization)', () => {
  afterEach(clearCookie);

  it('is unknown until the visitor answers', () => {
    expect(new WebConsent().state).toBe('unknown');
  });

  it('stores a grant in the shared cookie and announces it', () => {
    const seen: string[] = [];
    const listener = (e: Event) => seen.push((e as CustomEvent<{ state: string }>).detail.state);
    document.addEventListener('eq:consent', listener);
    try {
      new WebConsent().grant();
    } finally {
      document.removeEventListener('eq:consent', listener);
    }
    expect(document.cookie).toContain(`${CONSENT_COOKIE}=granted`);
    expect(new WebConsent().state).toBe('granted');
    expect(seen).toEqual(['granted']);
  });

  it('stores a denial too — a "no" asked again on every visit is the dark pattern', () => {
    new WebConsent().deny();
    expect(new WebConsent().state).toBe('denied');
  });

  it('treats a tampered cookie as unanswered rather than as consent', () => {
    document.cookie = `${CONSENT_COOKIE}=yes-please; path=/`;
    expect(new WebConsent().state).toBe('unknown');
  });

  it('treats a malformed escape as unanswered instead of throwing mid-render', () => {
    document.cookie = `${CONSENT_COOKIE}=%E0%A4%A; path=/`;
    expect(new WebConsent().state).toBe('unknown');
  });
});
