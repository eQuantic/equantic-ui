/**
 * Remaps a JavaScript stack trace back to original (C#) frames using source maps.
 * The map fetcher is injected so the logic is testable without a browser/network.
 */

import { SourceMapConsumer, RawSourceMap } from './source-map';

export interface RemappedFrame {
  /** Function name (original name from the map when available, else the JS frame's). */
  func: string | null;
  /** File path — the original (C#) source when mapped, otherwise the JS URL. */
  file: string;
  /** 1-based line. */
  line: number;
  /** 0-based column. */
  column: number;
  /** True when the frame was successfully mapped back to the original source. */
  mapped: boolean;
  /** The original source text, when the map embedded it (for showing a C# snippet). */
  sourceContent: string | null;
}

interface ParsedFrame {
  func: string | null;
  url: string;
  line: number;
  column: number; // 1-based, as browsers report
}

/** Fetches and parses the source map for a generated JS URL, or null when unavailable. */
export type MapFetcher = (jsUrl: string) => Promise<RawSourceMap | null>;

/** Parses one stack-trace line (Chrome `at f (url:l:c)` / `at url:l:c`, or Firefox `f@url:l:c`). */
export function parseFrame(line: string): ParsedFrame | null {
  const trimmed = line.trim();

  // Trailing "url:line:column" (url may contain ':' — greedy match backtracks to the last two).
  // '@' is excluded from the url so a Firefox "func@url" frame doesn't fold the name into the url.
  const loc = /(?:^|\(|@|\s)((?:[a-zA-Z][\w+.-]*:\/\/)?[^()\s@]+):(\d+):(\d+)\)?$/.exec(trimmed);
  if (!loc) return null;

  const url = loc[1];
  const lineNo = parseInt(loc[2], 10);
  const colNo = parseInt(loc[3], 10);
  if (!url || Number.isNaN(lineNo) || Number.isNaN(colNo)) return null;

  let func: string | null = null;
  const chrome = /^at\s+(.+?)\s+\(/.exec(trimmed);
  const firefox = /^(.*?)@/.exec(trimmed);
  if (chrome) func = chrome[1].trim();
  else if (firefox && firefox[1].trim()) func = firefox[1].trim();

  return { func: func || null, url, line: lineNo, column: colNo };
}

export async function remapStackTrace(
  stack: string,
  fetchMap: MapFetcher,
): Promise<RemappedFrame[]> {
  const consumers = new Map<string, SourceMapConsumer | null>();
  const frames: RemappedFrame[] = [];

  for (const rawLine of stack.split('\n')) {
    const parsed = parseFrame(rawLine);
    if (!parsed) continue;

    if (!consumers.has(parsed.url)) {
      let consumer: SourceMapConsumer | null = null;
      try {
        const raw = await fetchMap(parsed.url);
        consumer = raw ? new SourceMapConsumer(raw) : null;
      } catch {
        consumer = null;
      }
      consumers.set(parsed.url, consumer);
    }

    const consumer = consumers.get(parsed.url) ?? null;
    // Browser columns are 1-based; source maps are 0-based.
    const original =
      consumer?.originalPositionFor(parsed.line, Math.max(0, parsed.column - 1)) ?? null;

    if (original) {
      frames.push({
        func: original.name ?? parsed.func,
        file: original.source,
        line: original.line,
        column: original.column,
        mapped: true,
        sourceContent: original.sourceContent,
      });
    } else {
      frames.push({
        func: parsed.func,
        file: parsed.url,
        line: parsed.line,
        column: parsed.column,
        mapped: false,
        sourceContent: null,
      });
    }
  }

  return frames;
}
