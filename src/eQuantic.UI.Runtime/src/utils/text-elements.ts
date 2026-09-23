/**
 * TEXT ELEMENTS, the way .NET's `StringInfo` counts them: extended grapheme clusters (UAX #29), a
 * base with every mark, joiner and modifier that belongs to it. `e` followed by U+0301 is one, a
 * flag is two regional indicators and one element, a family emoji is five code points and one
 * element, and CR LF is one. The transpiled `StringInfo.ParseCombiningCharacters` and
 * `GetNextTextElementLength` land here, and both sides ask their PLATFORM the same annex: .NET its
 * own tables, the browser `Intl.Segmenter`.
 *
 * Where `Intl.Segmenter` is missing (Firefox before 125), the elements are approximated by the rules
 * that carry nearly all real text: marks, joiners and their next code point, emoji modifiers,
 * regional indicator pairs and CR LF stay with what comes before them.
 */

interface GraphemeSegmenter {
  segment(input: string): Iterable<{ segment: string; index: number }>;
}

type SegmenterConstructor = new (
  locales: string | undefined,
  options: { granularity: 'grapheme' },
) => GraphemeSegmenter;

let segmenter: GraphemeSegmenter | null | undefined;

/** The platform's grapheme segmenter, or null where the platform has none. */
function platformSegmenter(): GraphemeSegmenter | null {
  if (segmenter === undefined) {
    const Segmenter = (Intl as unknown as { Segmenter?: SegmenterConstructor }).Segmenter;
    segmenter = typeof Segmenter === 'function' ? new Segmenter(undefined, { granularity: 'grapheme' }) : null;
  }
  return segmenter;
}

const MARK = /^\p{M}$/u;

/** Whether the code point `current` stays in the element `previous` belongs to (the fallback). */
function joins(previous: number, current: number, indicatorRun: number): boolean {
  if (previous === 0x0d && current === 0x0a) return true;
  if (current === 0x200d || previous === 0x200d) return true;
  if (current >= 0x1f3fb && current <= 0x1f3ff) return true;
  if (current >= 0x1f1e6 && current <= 0x1f1ff && indicatorRun % 2 === 1) return true;
  return MARK.test(String.fromCodePoint(current));
}

/** Where each text element of `text` begins: `StringInfo.ParseCombiningCharacters`. */
export function textElementStarts(text: string): number[] {
  const platform = platformSegmenter();
  if (!platform) return approximateTextElementStarts(text);
  const starts: number[] = [];
  for (const { index } of platform.segment(text)) starts.push(index);
  return starts;
}

/** The elements where the platform has no segmenter (see the module's comment). Exported so its
 * tests reach it on a platform that has one. */
export function approximateTextElementStarts(text: string): number[] {
  const starts: number[] = [];
  let previous = -1;
  let indicatorRun = 0;
  for (let index = 0; index < text.length; ) {
    const current = text.codePointAt(index)!;
    if (previous < 0 || !joins(previous, current, indicatorRun)) starts.push(index);
    indicatorRun = current >= 0x1f1e6 && current <= 0x1f1ff ? indicatorRun + 1 : 0;
    previous = current;
    index += current > 0xffff ? 2 : 1;
  }
  return starts;
}

/**
 * How many UTF-16 units the text element starting at `index` spans, 0 at the very end:
 * `StringInfo.GetNextTextElementLength`. The index is taken as a boundary, as .NET takes it, and an
 * index past the end throws where .NET does.
 */
export function nextTextElementLength(text: string, index: number): number {
  if (index < 0 || index > text.length) {
    throw new RangeError('Index was out of range. Must be non-negative and less than or equal to the size of the collection.');
  }
  if (index === text.length) return 0;
  const rest = text.slice(index);
  const platform = platformSegmenter();
  if (platform) {
    for (const { segment } of platform.segment(rest)) return segment.length;
    return 0;
  }
  const starts = approximateTextElementStarts(rest);
  return starts.length > 1 ? starts[1] : rest.length;
}
