/**
 * The query policy, client side. Its twin is `RouteValuesQueryPolicyTests` in the server suite, and
 * the pair exists because the two sides used to answer differently and nothing said so:
 * `?tag=a&tag=b` was `"a,b"` from the server (ASP.NET's `StringValues.ToString()` comma-joining)
 * and `"a"` here, and collapsing onto one route type nearly made it `"b"` — `Object.fromEntries`
 * keeps the LAST value, which is how this was noticed.
 */

import { describe, expect, it } from 'vitest';
import { RouteValues } from './route-values';

describe('RouteValues query policy', () => {
  it('a repeated key means its first value', () => {
    const route = RouteValues.from({}, new URLSearchParams('tag=a&tag=b'));

    expect(route.query('tag')).toBe('a');
  });

  it('a single key is unchanged', () => {
    expect(RouteValues.from({}, new URLSearchParams('page=2')).query('page')).toBe('2');
  });

  // An empty FIRST value is still the answer, not a reason to look at the next one.
  it('an empty first value is still the answer', () => {
    expect(RouteValues.from({}, new URLSearchParams('tag=&tag=b')).query('tag')).toBe('');
  });

  it('a key that is not there is null, not undefined', () => {
    expect(RouteValues.from({}, new URLSearchParams('page=2')).query('tag')).toBeNull();
  });

  // A query key is whatever a VISITOR typed, and `pairs['__proto__'] = v` on a plain `{}` calls
  // Object.prototype's setter instead of creating an own property. Measured before the guard:
  // this answered null while the server answered 'x'. Found in review.
  it('a key named __proto__ is a key like any other', () => {
    const route = RouteValues.from({}, new URLSearchParams('__proto__=x&tag=a'));

    expect(route.query('__proto__')).toBe('x');
    expect(route.query('tag')).toBe('a', 'and the ordinary key beside it still works');
  });

  it('…and so is a route parameter named __proto__', () => {
    const captured: Record<string, string> = Object.create(null);
    captured['__proto__'] = 'y';

    expect(RouteValues.from(captured, new URLSearchParams()).param('__proto__')).toBe('y');
  });

  // The record a caller hands over is COPIED, so a later mutation cannot change a route a page has
  // already been given.
  it('a route does not change under a page when its source object does', () => {
    const captured: Record<string, string> = Object.create(null);
    captured['slug'] = 'first';
    const route = RouteValues.from(captured, new URLSearchParams());
    captured['slug'] = 'second';

    expect(route.param('slug')).toBe('first');
  });

  it('route parameters come through as they are', () => {
    expect(RouteValues.from({ slug: 'getting-started' }, new URLSearchParams()).param('slug')).toBe(
      'getting-started',
    );
  });
});
