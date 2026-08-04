import { afterEach, describe, expect, it, vi } from 'vitest';
import { WebMotionSensor } from './motion-sensor';

/** The browser realization of IMotionSensor — frames, units, and the honest availability guess. */
describe('WebMotionSensor', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('is unavailable where there is no touch — a desktop browser has the event and no sensor', () => {
    vi.stubGlobal('navigator', { maxTouchPoints: 0 });
    expect(new WebMotionSensor().isAvailable).toBe(false);
  });

  it('normalizes units to the C# contract: g for acceleration, radians for rotation', () => {
    const sensor = new WebMotionSensor();
    const seen: unknown[] = [];
    sensor.subscribe((reading) => seen.push(reading));

    // One g straight down, turning 180°/s about z — in the browser's own units.
    const event = new Event('devicemotion') as DeviceMotionEvent & Record<string, unknown>;
    Object.assign(event, {
      acceleration: { x: 0, y: 0, z: 0 },
      accelerationIncludingGravity: { x: 0, y: 0, z: -9.80665 },
      rotationRate: { alpha: 180, beta: 0, gamma: 0 },
    });
    window.dispatchEvent(event);

    expect(seen).toHaveLength(1);
    const reading = seen[0] as {
      gravity: { z: number };
      rotationRate: { z: number };
    };
    expect(reading.gravity.z).toBeCloseTo(-1, 5);
    expect(reading.rotationRate.z).toBeCloseTo(Math.PI, 5);
  });

  it('the last dispose stops the hardware-side listener', () => {
    const sensor = new WebMotionSensor();
    const seen: unknown[] = [];
    const subscription = sensor.subscribe((reading) => seen.push(reading));
    subscription.dispose();
    subscription.dispose();

    window.dispatchEvent(new Event('devicemotion'));
    expect(seen).toEqual([]);
  });
});
