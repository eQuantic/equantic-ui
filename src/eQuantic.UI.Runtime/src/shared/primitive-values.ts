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

import { exception } from '../utils/exceptions';
import { hashesByValue } from '../utils/hash';
import type { HydrationSpec } from '../utils/hydrate';

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

/**
 * The C# `ServerTopic<T>` — a topic's name, and the hydration spec of what it carries. `T` is erased
 * here, so eqc passes its spec after the name (`[HydratesTypeArgument]`), the one a Server Action's
 * result is revived with, and `IServerEvents` revives every payload published to the topic with it.
 * Null where the payload needs no revival. Two topics are equal when their names and specs are, as two
 * C# topics are when their names and payload types are.
 */
export class ServerTopic {
  constructor(
    readonly name: string,
    readonly spec: HydrationSpec | null = null,
  ) {
    // `ArgumentException.ThrowIfNullOrEmpty(name)`, as the C# constructor refuses it.
    if (name == null)
      throw exception('System.ArgumentNullException', "Value cannot be null. (Parameter 'name')");
    if (name.length === 0)
      throw exception(
        'System.ArgumentException',
        "The value cannot be an empty string. (Parameter 'name')",
      );
  }

  toString(): string {
    return this.name;
  }

  /** Where a topic that crossed the wire keeps the spec it revives with (`hydrate`). */
  static readonly $typeArguments = ['spec'] as const;
}

/**
 * The C# `ServerConnection` — where the page's connection to the server's events stands, as the
 * `ServerConnectionState` enum's wire string ('disconnected' | 'connecting' | 'connected' |
 * 'reconnecting'), and the id of the last event received.
 */
export class ServerConnection {
  /** With no arguments, the C# struct's zero: disconnected, no event received (`[ZeroConstructs]`). */
  constructor(
    readonly state: string = 'disconnected',
    readonly lastEventId: string | null = null,
  ) {}

  static readonly disconnected = new ServerConnection('disconnected', null);
}

/** The C# `ServerTopicRefusal` — a topic the server did not bind, and why, as the
 * `ServerTopicRefusalReason` enum's wire string ('forbidden' | 'unknown' | 'limitReached' | 'failed'). */
export class ServerTopicRefusal {
  readonly topic: string;
  readonly reason: string;

  /** With no arguments, the C# struct's zero: no topic, and the enum's first reason (`[ZeroConstructs]`). */
  constructor(topic: string | null = null, reason = 'forbidden') {
    this.topic = topic as string;
    this.reason = reason;
  }
}

/** The twins of the vocabulary's records and structs: each hashes by its members, as `equals`
 * compares it (`ValueTwinHashTests` holds this list to the C#). */
hashesByValue(
  ImageData,
  GeoLocation,
  MotionVector,
  MotionReading,
  NetworkState,
  SpringSpec,
  MotionSpec,
  ServerTopic,
  ServerConnection,
  ServerTopicRefusal,
);
