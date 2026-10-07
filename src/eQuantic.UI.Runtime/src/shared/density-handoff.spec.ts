import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { DENSITY_COOKIE } from './markers';
import { pointerDensity, rememberDensity } from './photon-context';

/**
 * The density the SERVER is told (#623). It cannot see the pointer, so it rendered every page
 * Comfortable while the client lowered Compact under a mouse, and a page showed a mix of both. The
 * runtime reads the pointer and leaves the answer in a SESSION cookie, which the server renders at.
 */
describe('the density the server is told', () => {
  let written: string[];

  beforeEach(() => {
    written = [];
    // Wherever the DOM defines it on the chain: the accessor is the environment's, not ours.
    let owner: object | null = Object.getPrototypeOf(document);
    while (owner && !Object.getOwnPropertyDescriptor(owner, 'cookie')) owner = Object.getPrototypeOf(owner);
    const native = Object.getOwnPropertyDescriptor(owner!, 'cookie')!;
    Object.defineProperty(document, 'cookie', {
      configurable: true,
      get: () => native.get!.call(document),
      set: (value: string) => {
        written.push(value);
        native.set!.call(document, value);
      },
    });
    document.cookie = `${DENSITY_COOKIE}=; max-age=0; path=/`;
    written = [];
  });

  afterEach(() => {
    delete (document as unknown as Record<string, unknown>).cookie;
    vi.unstubAllGlobals();
  });

  const pointer = (fine: boolean) =>
    vi.stubGlobal('matchMedia', (query: string) => ({ matches: query === '(pointer: fine)' && fine }));

  it('is what the pointer asks for', () => {
    pointer(true);
    expect(pointerDensity()).toBe('compact');
    pointer(false);
    expect(pointerDensity()).toBe('comfortable');
  });

  it('is left in a SESSION cookie, written once', () => {
    rememberDensity('compact');
    rememberDensity('compact');

    expect(written).toEqual([`${DENSITY_COOKIE}=compact; path=/; samesite=lax`]);
    expect(document.cookie).toContain(`${DENSITY_COOKIE}=compact`);
  });

  it('is written again when the pointer changes', () => {
    rememberDensity('compact');
    rememberDensity('comfortable');

    expect(written).toHaveLength(2);
    expect(document.cookie).toContain(`${DENSITY_COOKIE}=comfortable`);
  });
});
