/**
 * Minimal Source Map (V3) consumer for the dev error overlay.
 *
 * Translates a generated (JavaScript) position back to its original position. For eQuantic.UI the
 * "original" is the C# source — the build composes the C#→TS and TS→JS maps (merge-maps.js) into a
 * single JS→C# map whose `sourcesContent` holds the C# files — so this is what lets the overlay show
 * a C# stack trace instead of JavaScript.
 */

export interface RawSourceMap {
  version: number;
  sources: string[];
  sourcesContent?: (string | null)[];
  names?: string[];
  mappings: string;
}

export interface OriginalPosition {
  /** Original source path (e.g. the C# file). */
  source: string;
  /** 1-based original line. */
  line: number;
  /** 0-based original column. */
  column: number;
  /** Original symbol name, if the segment carried one. */
  name: string | null;
  /** The original source text, if the map embedded it (sourcesContent). */
  sourceContent: string | null;
}

const BASE64 = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/';
const CHAR_TO_INT: Record<string, number> = {};
for (let i = 0; i < BASE64.length; i++) CHAR_TO_INT[BASE64.charAt(i)] = i;

/** Decodes a single Base64 VLQ segment into its integer fields. */
export function decodeVlq(segment: string): number[] {
  const values: number[] = [];
  let shift = 0;
  let value = 0;

  for (const ch of segment) {
    const digit = CHAR_TO_INT[ch];
    if (digit === undefined) continue;

    const hasContinuation = (digit & 0x20) !== 0;
    value += (digit & 0x1f) << shift;

    if (hasContinuation) {
      shift += 5;
    } else {
      const negative = (value & 1) === 1;
      value >>>= 1;
      values.push(negative ? -value : value);
      shift = 0;
      value = 0;
    }
  }

  return values;
}

// A decoded mapping segment: [generatedColumn, sourceIndex, sourceLine, sourceColumn, nameIndex?].
type Segment = [number, number?, number?, number?, number?];

export class SourceMapConsumer {
  private readonly sources: string[];
  private readonly sourcesContent: (string | null)[];
  private readonly names: string[];
  /** Segments per generated line (0-based), each line's segments sorted by generated column. */
  private readonly lines: Segment[][] = [];

  constructor(raw: RawSourceMap) {
    this.sources = raw.sources ?? [];
    this.sourcesContent = raw.sourcesContent ?? [];
    this.names = raw.names ?? [];
    this.parse(raw.mappings ?? '');
  }

  private parse(mappings: string): void {
    // Source index / line / column / name index are cumulative across the whole map.
    let sourceIndex = 0;
    let sourceLine = 0;
    let sourceColumn = 0;
    let nameIndex = 0;

    const groups = mappings.split(';');
    for (let gl = 0; gl < groups.length; gl++) {
      let generatedColumn = 0; // resets per generated line
      const segments: Segment[] = [];
      const group = groups[gl];

      if (group.length > 0) {
        for (const segStr of group.split(',')) {
          if (segStr.length === 0) continue;
          const fields = decodeVlq(segStr);
          generatedColumn += fields[0];
          const segment: Segment = [generatedColumn];

          if (fields.length >= 4) {
            sourceIndex += fields[1];
            sourceLine += fields[2];
            sourceColumn += fields[3];
            segment[1] = sourceIndex;
            segment[2] = sourceLine;
            segment[3] = sourceColumn;
            if (fields.length >= 5) {
              nameIndex += fields[4];
              segment[4] = nameIndex;
            }
          }
          segments.push(segment);
        }
      }
      this.lines[gl] = segments;
    }
  }

  /**
   * Maps a 1-based generated line and 0-based generated column to the original position,
   * or null if there is no mapping (e.g. a runtime-only line).
   */
  originalPositionFor(generatedLine: number, generatedColumn: number): OriginalPosition | null {
    const segments = this.lines[generatedLine - 1];
    if (!segments || segments.length === 0) return null;

    // Find the last segment whose generated column is <= the requested column.
    let lo = 0;
    let hi = segments.length - 1;
    let found = -1;
    while (lo <= hi) {
      const mid = (lo + hi) >> 1;
      if (segments[mid][0] <= generatedColumn) {
        found = mid;
        lo = mid + 1;
      } else {
        hi = mid - 1;
      }
    }
    if (found < 0) found = 0;

    const seg = segments[found];
    if (seg.length < 4 || seg[1] === undefined) return null;

    const srcIdx = seg[1];
    return {
      source: this.sources[srcIdx] ?? '<unknown>',
      line: (seg[2] ?? 0) + 1,
      column: seg[3] ?? 0,
      name: seg[4] !== undefined ? (this.names[seg[4]] ?? null) : null,
      sourceContent: this.sourcesContent[srcIdx] ?? null,
    };
  }
}
