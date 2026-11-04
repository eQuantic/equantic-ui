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
import { isWhiteSpace } from '../utils/white-space';
import { endsWith } from '../utils/string-search';
import { ordinalIgnoreCase } from '../utils/string-statics';
import type { HydrationSpec } from '../utils/hydrate';
import type { CurveValue } from './nodes';
import type { PasskeyOutcomeValue } from './enums.generated';

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

/**
 * The C# `PasskeyResult` — how a passkey ceremony ended, and on success the credential as the
 * relying party's Web Authentication JSON. `outcome` is the `PasskeyOutcome` enum as its wire string,
 * and `'unavailable'`, the enum's zero, is what a result nobody filled in reads as.
 */
export class PasskeyResult {
  constructor(
    readonly outcome: PasskeyOutcomeValue = 'unavailable',
    readonly credential: string | null = null,
  ) {}
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

/** The C# `MotionSpec` — a duration and the curve it runs on, the curve's own data (`Curve.standard`
 * is `{ x1: 0.2…, y1: 0, x2: 0, y2: 1 }`). It was declared a preset NAME until #518, a shape no C#
 * `MotionSpec` has. The design system's motion roles (`Motion.press` …) are instances of it. */
export class MotionSpec {
  constructor(
    readonly durationMs: number,
    readonly curve: CurveValue,
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

/**
 * The C# `FileFilter` — one entry of a picker's format list: the label a person reads and the
 * extensions it stands for, without the dot. C# takes the extensions as `params`, and eqc passes each
 * one as an argument of its own, so they are gathered here as C# gathers them.
 */
export class FileFilter {
  readonly extensions: readonly string[];

  constructor(
    readonly label: string,
    ...extensions: string[]
  ) {
    this.extensions = extensions;
  }
}

/** `ArgumentNullException.ThrowIfNull`, as a C# constructor refuses a null. */
function refuseNull(value: unknown, parameter: string): void {
  if (value == null)
    throw exception('System.ArgumentNullException', `Value cannot be null. (Parameter '${parameter}')`);
}

/**
 * The C# `UploadPolicy` — a kind of file the app takes from a person: its name, the formats it
 * accepts (empty for any file) and the largest file it takes, a `long` and so a BigInt here. The page
 * reads it to say what it accepts; the server holds every upload to it. It is a declaration rather than
 * a value, a class in C#, so two policies are equal when they are the same object.
 */
export class UploadPolicy {
  constructor(
    readonly name: string,
    readonly filters: readonly FileFilter[],
    readonly maxSize: bigint,
    readonly requireSignedIn = false,
  ) {
    // As the C# constructor refuses them, in its order.
    refuseNull(name, 'name');
    if ([...name].every(isWhiteSpace))
      throw exception(
        'System.ArgumentException',
        "The value cannot be an empty string or composed entirely of whitespace. (Parameter 'name')",
      );
    refuseNull(filters, 'filters');
    if (maxSize <= 0n)
      throw exception(
        'System.ArgumentOutOfRangeException',
        `maxSize ('${maxSize}') must be a non-negative and non-zero value. (Parameter 'maxSize')\nActual value was ${maxSize}.`,
      );
  }

  /**
   * Whether the policy accepts a file of this name: any file without filters, otherwise one whose name
   * ENDS in one of its extensions, compared without case, a leading dot in a filter read as absent. The
   * C# `Accepts`, which the server asks too.
   */
  accepts(fileName: string): boolean {
    refuseNull(fileName, 'fileName');
    if (this.filters.length === 0) return true;
    for (const filter of this.filters)
      for (const extension of filter.extensions) {
        const bare = trimDots(extension);
        if (bare.length > 0 && endsWith(fileName, '.' + bare, 'ordinalIgnoreCase')) return true;
      }
    return false;
  }

  /**
   * Why the policy refuses a file of this name and size, in the words the server answers with, or null
   * when it accepts it: a format it does not name first, then a size past its largest. The C#
   * `RefusalOf`.
   */
  refusalOf(fileName: string, size: bigint): string | null {
    if (!this.accepts(fileName)) return `Only ${this.formats()} files are accepted here.`;
    if (size > this.maxSize) return `The file is larger than the ${this.maxSize} bytes accepted here.`;
    return null;
  }

  /** The extensions the policy accepts, as a refusal names them: `.stl, .obj`. */
  private formats(): string {
    const formats: string[] = [];
    for (const filter of this.filters)
      for (const extension of filter.extensions) {
        const dotted = '.' + trimDots(extension);
        const seen = formats.some(
          (format) => ordinalIgnoreCase(format, 0, format.length, dotted, 0, dotted.length) === 0,
        );
        if (!seen) formats.push(dotted);
      }
    return formats.join(', ');
  }
}

/** `string.TrimStart('.')`. */
function trimDots(text: string): string {
  let start = 0;
  while (start < text.length && text[start] === '.') start++;
  return text.slice(start);
}

/**
 * The C# `UploadedFile` — a file a person uploaded, as the page knows it: the id the server gave the
 * upload, its name, type, size (a `long`, a BigInt here) and the name of its policy. A Server Action
 * that takes one is handed the id, and the server answers the rest from its own record. Reading the
 * content and deleting it are the server's (`[ServerOnly]`), so this twin has neither.
 */
export class UploadedFile {
  constructor(
    readonly id: string,
    readonly name: string,
    readonly mimeType: string,
    readonly size: bigint,
    readonly policy: string,
  ) {
    // As the C# constructor refuses them, in its order.
    refuseNull(id, 'id');
    if ([...id].every(isWhiteSpace))
      throw exception(
        'System.ArgumentException',
        "The value cannot be an empty string or composed entirely of whitespace. (Parameter 'id')",
      );
    refuseNull(name, 'name');
    refuseNull(mimeType, 'mimeType');
    if (size < 0n)
      throw exception(
        'System.ArgumentOutOfRangeException',
        `size ('${size}') must be a non-negative value. (Parameter 'size')\nActual value was ${size}.`,
      );
    refuseNull(policy, 'policy');
  }
}

/** The twins of the vocabulary's records and structs: each hashes by its members, as `equals`
 * compares it (`ValueTwinHashTests` holds this list to the C#). */
hashesByValue(
  FileFilter,
  ImageData,
  GeoLocation,
  MotionVector,
  MotionReading,
  NetworkState,
  PasskeyResult,
  SpringSpec,
  MotionSpec,
  ServerTopic,
  ServerConnection,
  ServerTopicRefusal,
);
