import { describe, expect, it } from 'vitest';

import { hydrate } from './hydrate';

/**
 * The STRUCTURAL half of the typed boundary: a domain record from a referenced assembly has no twin
 * to name, so its spec names the MEMBERS instead, and the plain object is coerced in place. Found
 * by the web site: an array of foreign records carried `downloads: "121892"`, and the first
 * division met "Cannot mix BigInt and other types" — in the browser only, after a build the
 * compiler had accepted and a page the server had rendered perfectly.
 */
describe('members specs coerce a twinless object in place', () => {
  it('coerces exactly the named members and copies the rest', () => {
    const incoming = { id: 'eQuantic.Core.Data', downloads: '121892', isPrerelease: false };

    const hydrated = hydrate(incoming, { members: { downloads: 'long' } }) as Record<string, unknown>;

    expect(hydrated.downloads).toBe(121892n);
    expect(hydrated.id).toBe('eQuantic.Core.Data');
    expect(hydrated.isPrerelease).toBe(false);
  });

  it('composes under a list spec, which is how an array of records arrives', () => {
    const hydrated = hydrate(
      [{ downloads: '9007199254740993' }],
      [{ members: { downloads: 'long' } }],
    ) as Array<Record<string, unknown>>;

    expect(hydrated[0].downloads).toBe(9007199254740993n);
  });

  it('nests: a member can itself be a members spec', () => {
    const hydrated = hydrate(
      { stats: { total: '5' } },
      { members: { stats: { members: { total: 'long' } } } },
    ) as { stats: { total: unknown } };

    expect(hydrated.stats.total).toBe(5n);
  });

  it('passes non-objects through untouched', () => {
    expect(hydrate('x', { members: { a: 'long' } })).toBe('x');
    expect(hydrate(null, { members: { a: 'long' } })).toBeNull();
  });
});
