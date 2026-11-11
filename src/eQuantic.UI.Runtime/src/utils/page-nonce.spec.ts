import { afterEach, describe, expect, it } from 'vitest';
import { createStyleElement, pageNonce } from './page-nonce';

// The style elements the runtime adds carry the page's nonce (#1268): under a strict
// Content-Security-Policy one without it is refused and gets no sheet, so every rule the runtime
// inserts into it is lost. Measured in Chromium 152: a created style refused without the nonce,
// applied with it.
describe('the page nonce', () => {
  afterEach(() => {
    document.head.querySelectorAll('[data-nonce-test]').forEach((element) => element.remove());
  });

  function carrier(nonce: string): void {
    const script = document.createElement('script');
    script.setAttribute('data-nonce-test', '');
    script.nonce = nonce;
    script.setAttribute('nonce', nonce);
    document.head.appendChild(script);
  }

  it('is read off the first element the server stamped', () => {
    carrier('n0nce');
    expect(pageNonce()).toBe('n0nce');
  });

  it('is empty on a page served without one', () => {
    expect(pageNonce()).toBe('');
  });

  it('is carried by every style element the runtime creates', () => {
    carrier('n0nce');
    const style = createStyleElement('eq-nonce-probe');
    style.setAttribute('data-nonce-test', '');
    expect(style.nonce).toBe('n0nce');
    expect(document.getElementById('eq-nonce-probe')).toBe(style);
  });

  it('leaves a style element without one when the page has none', () => {
    const style = createStyleElement('eq-nonce-probe');
    style.setAttribute('data-nonce-test', '');
    expect(style.nonce ?? '').toBe('');
  });
});
