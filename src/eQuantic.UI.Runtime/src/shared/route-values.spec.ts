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

  it('route parameters come through as they are', () => {
    expect(RouteValues.from({ slug: 'getting-started' }, new URLSearchParams()).param('slug')).toBe(
      'getting-started',
    );
  });
});
