/**
 * The browser's ILocation — navigator.geolocation, which has carried this exact contract since
 * before most of the platforms existed: asking IS the permission flow, and the prompt is the
 * browser's own.
 *
 * `permission` is a CACHE kept honest lazily: the Permissions API answers asynchronously, so the
 * property starts at notDetermined, a query refreshes it in the background, and every answered or
 * refused request updates it for certain. A page reading it before ever asking sees notDetermined,
 * which is also the truth.
 */

export interface GeoLocationValue {
  latitude: number;
  longitude: number;
  accuracyMeters: number;
}

interface Subscription {
  dispose(): void;
}

export class WebLocation {
  private state: 'notDetermined' | 'granted' | 'denied' = 'notDetermined';

  constructor() {
    // Best-effort refresh; browsers without the Permissions API just start unknown.
    if (typeof navigator !== 'undefined' && navigator.permissions?.query) {
      navigator.permissions
        .query({ name: 'geolocation' })
        .then((status) => {
          if (status.state === 'granted') this.state = 'granted';
          else if (status.state === 'denied') this.state = 'denied';
        })
        .catch(() => {
          /* unknown stays notDetermined */
        });
    }
  }

  get isAvailable(): boolean {
    return typeof navigator !== 'undefined' && !!navigator.geolocation;
  }

  get permission(): 'notDetermined' | 'granted' | 'denied' {
    return this.state;
  }

  getCurrentAsync(): Promise<GeoLocationValue | null> {
    if (!this.isAvailable) return Promise.resolve(null);
    return new Promise((resolve) => {
      navigator.geolocation.getCurrentPosition(
        (position) => {
          this.state = 'granted';
          resolve(read(position));
        },
        (error) => {
          if (error.code === error.PERMISSION_DENIED) this.state = 'denied';
          resolve(null); // denied, off, or no fix — the contract's null, never a rejection
        },
        { timeout: 15_000 },
      );
    });
  }

  subscribe(onChanged: (location: GeoLocationValue) => void): Subscription {
    if (!this.isAvailable) return { dispose: () => {} };
    const watch = navigator.geolocation.watchPosition(
      (position) => {
        this.state = 'granted';
        onChanged(read(position));
      },
      (error) => {
        if (error.code === error.PERMISSION_DENIED) this.state = 'denied';
      },
    );
    return { dispose: () => navigator.geolocation.clearWatch(watch) };
  }
}

function read(position: GeolocationPosition): GeoLocationValue {
  return {
    latitude: position.coords.latitude,
    longitude: position.coords.longitude,
    accuracyMeters: position.coords.accuracy,
  };
}
