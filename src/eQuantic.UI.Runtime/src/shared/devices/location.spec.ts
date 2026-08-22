import { afterEach, describe, expect, it, vi } from 'vitest';
import { WebLocation } from './location';

/** The browser realization of ILocation — the permission cache and the null-for-everything-bad rule. */
describe('WebLocation', () => {
  afterEach(() => vi.unstubAllGlobals());

  function stubGeolocation(impl: Partial<Geolocation>): void {
    vi.stubGlobal('navigator', { geolocation: impl });
  }

  it('a fix resolves the value and settles the permission as granted', async () => {
    stubGeolocation({
      getCurrentPosition: (ok) =>
        ok({
          coords: { latitude: 41.1, longitude: -8.6, accuracy: 35 },
        } as GeolocationPosition),
    });
    const location = new WebLocation();

    const fix = await location.getCurrentAsync();
    expect(fix).toEqual({ latitude: 41.1, longitude: -8.6, accuracyMeters: 35 });
    expect(location.permission).toBe('granted');
  });

  it('a refusal resolves null — never a rejection — and settles denied', async () => {
    stubGeolocation({
      getCurrentPosition: (_ok, fail) =>
        fail!({
          code: 1,
          PERMISSION_DENIED: 1,
          POSITION_UNAVAILABLE: 2,
          TIMEOUT: 3,
        } as GeolocationPositionError),
    });
    const location = new WebLocation();

    expect(await location.getCurrentAsync()).toBeNull();
    expect(location.permission).toBe('denied');
  });

  it('disposing a watch clears it with the browser', () => {
    const cleared: number[] = [];
    stubGeolocation({
      watchPosition: () => 7,
      clearWatch: (id) => cleared.push(id),
    });

    new WebLocation().subscribe(() => {}).dispose();
    expect(cleared).toEqual([7]);
  });
});
