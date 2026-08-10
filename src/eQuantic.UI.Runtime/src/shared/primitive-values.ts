/**
 * Client twins of the Primitives VALUE types a page can name.
 *
 * eqc routes the whole `eQuantic.UI.Primitives` namespace to `@equantic/runtime` implicitly, so
 * naming one of these in a page emits an import of that name. Without a twin the page dies at
 * hydration on "does not provide an export named 'X'" while SSR keeps answering 200 with correct
 * markup — the failure `RouteValues` shipped with, and the reason `primitives-exports.spec.ts`
 * exists.
 *
 * These are the ones a page reaches for on its own: a picked photo, a location fix, a network
 * reading, a motion sample, a spring. Shapes only — the behaviour lives on the capability
 * interfaces, which are resolved by name and never imported.
 */

/** The C# `ImageData` — a picture the user chose, as bytes plus what they are. */
export class ImageData {
  constructor(
    readonly bytes: Uint8Array,
    readonly mimeType: string,
    readonly width = 0,
    readonly height = 0,
    readonly name: string | null = null,
  ) {}

  /** How big the file is. Useful before deciding to upload it. */
  get byteCount(): number {
    return this.bytes.length;
  }
}

/** The C# `GeoLocation` — one position fix, with how much to trust it. */
export class GeoLocation {
  constructor(
    readonly latitude: number,
    readonly longitude: number,
    readonly accuracyMeters: number,
  ) {}
}

/** The C# `MotionVector` — one axis triple. */
export class MotionVector {
  constructor(
    readonly x: number,
    readonly y: number,
    readonly z: number,
  ) {}
}

/** The C# `MotionReading` — the accelerometer and gyroscope, fused by the platform. */
export class MotionReading {
  constructor(
    readonly acceleration: MotionVector,
    readonly gravity: MotionVector,
    readonly rotationRate: MotionVector,
  ) {}
}

/**
 * The C# `NetworkState` — reachable or not, and through what. `kind` is the `NetworkKind` enum as
 * its wire string ('none' | 'wifi' | 'cellular' | 'wired' | 'other').
 */
export class NetworkState {
  constructor(
    readonly online: boolean,
    readonly kind: string,
  ) {}

  static readonly offline = new NetworkState(false, 'none');
}

/** The C# `SpringSpec` — a spring by its physics, not by a duration. */
export class SpringSpec {
  constructor(
    readonly stiffness: number,
    readonly damping: number,
    readonly mass: number,
  ) {}

  static readonly default = new SpringSpec(380, 34, 1);
}

/** The C# `MotionSpec` — a duration and the curve it runs on ('standard' | 'emphasized' | …). */
export class MotionSpec {
  constructor(
    readonly durationMs: number,
    readonly curve: string,
  ) {}
}

/**
 * The C# `WindowSizeClasses` — the two thresholds and the classification, so a page can ask the
 * same question the adaptive nodes ask.
 */
export class WindowSizeClasses {
  static readonly mediumMinDp = 600;
  static readonly expandedMinDp = 840;

  /** 'compact' | 'medium' | 'expanded' — the `WindowSizeClass` enum as its wire string. */
  static fromWidth(dp: number): string {
    if (dp >= WindowSizeClasses.expandedMinDp) return 'expanded';
    if (dp >= WindowSizeClasses.mediumMinDp) return 'medium';
    return 'compact';
  }
}
