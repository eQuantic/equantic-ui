/**
 * The browser's IMotionSensor — DeviceMotionEvent, normalized to the C# contract's frame.
 *
 * Availability is a GUESS on the web, stated honestly: the event exists on desktop browsers that
 * will never fire it, so `isAvailable` requires both the constructor and a coarse touch check.
 * iOS Safari additionally gates the events behind `DeviceMotionEvent.requestPermission()`, which
 * MUST run inside a user gesture — the first subscribe asks if the API is there, and a page that
 * subscribes outside a tap simply never hears anything, exactly as Safari intends.
 *
 * Units: `accelerationIncludingGravity`/`acceleration` arrive in m/s²; the contract speaks g
 * (CoreMotion's unit) so values divide by 9.80665. Rotation rate arrives in degrees per second —
 * the ONE axis where the web disagrees with CoreMotion — and converts to radians.
 */

export interface MotionVectorValue {
  x: number;
  y: number;
  z: number;
}

export interface MotionReadingValue {
  acceleration: MotionVectorValue;
  gravity: MotionVectorValue;
  rotationRate: MotionVectorValue;
}

interface Subscription {
  dispose(): void;
}

const G = 9.80665;
const RADIANS = Math.PI / 180;

export class WebMotionSensor {
  private listeners = new Set<(reading: MotionReadingValue) => void>();
  private handler: ((e: DeviceMotionEvent) => void) | null = null;

  get isAvailable(): boolean {
    return (
      typeof window !== 'undefined' &&
      typeof DeviceMotionEvent !== 'undefined' &&
      (typeof navigator === 'undefined' || (navigator.maxTouchPoints ?? 0) > 0)
    );
  }

  subscribe(onReading: (reading: MotionReadingValue) => void): Subscription {
    this.listeners.add(onReading);
    if (this.listeners.size === 1) this.start();
    return {
      dispose: () => {
        this.listeners.delete(onReading);
        if (this.listeners.size === 0) this.stop();
      },
    };
  }

  private start(): void {
    if (typeof window === 'undefined' || typeof DeviceMotionEvent === 'undefined') return;

    const attach = () => {
      this.handler = (e) => this.deliver(e);
      window.addEventListener('devicemotion', this.handler);
    };

    // iOS Safari's gate. Elsewhere the function does not exist and the events just flow.
    const gate = (DeviceMotionEvent as unknown as { requestPermission?: () => Promise<string> })
      .requestPermission;
    if (typeof gate === 'function') {
      gate
        .call(DeviceMotionEvent)
        .then((answer) => {
          if (answer === 'granted') attach();
        })
        .catch(() => {
          /* asked outside a user gesture: Safari said no, and meant it */
        });
    } else {
      attach();
    }
  }

  private stop(): void {
    if (this.handler && typeof window !== 'undefined')
      window.removeEventListener('devicemotion', this.handler);
    this.handler = null;
  }

  private deliver(e: DeviceMotionEvent): void {
    const user = e.acceleration;
    const total = e.accelerationIncludingGravity;
    const rate = e.rotationRate;

    const acceleration = vector(user, G);
    // Gravity is what remains when the user's own hand is removed.
    const gravity: MotionVectorValue = {
      x: ((total?.x ?? 0) - (user?.x ?? 0)) / G,
      y: ((total?.y ?? 0) - (user?.y ?? 0)) / G,
      z: ((total?.z ?? 0) - (user?.z ?? 0)) / G,
    };
    const rotationRate: MotionVectorValue = {
      x: (rate?.beta ?? 0) * RADIANS,
      y: (rate?.gamma ?? 0) * RADIANS,
      z: (rate?.alpha ?? 0) * RADIANS,
    };

    const reading: MotionReadingValue = { acceleration, gravity, rotationRate };
    for (const listener of this.listeners) listener(reading);
  }
}

function vector(
  v: DeviceMotionEventAcceleration | null | undefined,
  divisor: number,
): MotionVectorValue {
  return { x: (v?.x ?? 0) / divisor, y: (v?.y ?? 0) / divisor, z: (v?.z ?? 0) / divisor };
}
