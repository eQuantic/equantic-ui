/**
 * The browser's answer to INetworkStatus — the C# contract's twin, resolved by interface name.
 *
 * `navigator.onLine` and the online/offline events are the whole reliable surface. The Network
 * Information API (`navigator.connection`) would name the KIND, but it never shipped outside
 * Chromium and is frozen there; reading it where it exists would make the same page answer
 * differently per browser. So the kind is honest instead: `other` while online, `none` while not —
 * unknown is not wifi.
 */

export interface NetworkStateValue {
  online: boolean;
  kind: 'none' | 'wifi' | 'cellular' | 'wired' | 'other';
}

interface Subscription {
  dispose(): void;
}

export class WebNetworkStatus {
  private listeners = new Set<(state: NetworkStateValue) => void>();
  private installed = false;

  get current(): NetworkStateValue {
    return this.read();
  }

  subscribe(onChanged: (state: NetworkStateValue) => void): Subscription {
    this.install();
    this.listeners.add(onChanged);
    return {
      dispose: () => {
        this.listeners.delete(onChanged);
      },
    };
  }

  private read(): NetworkStateValue {
    const online = typeof navigator === 'undefined' ? true : navigator.onLine;
    return { online, kind: online ? 'other' : 'none' };
  }

  /** One pair of window listeners for the process, however many components subscribe. */
  private install(): void {
    if (this.installed || typeof window === 'undefined') return;
    this.installed = true;
    const notify = () => {
      const state = this.read();
      for (const listener of this.listeners) listener(state);
    };
    window.addEventListener('online', notify);
    window.addEventListener('offline', notify);
  }
}
