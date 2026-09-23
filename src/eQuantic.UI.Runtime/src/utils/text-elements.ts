/**
 * TEXT ELEMENTS, the way .NET's `StringInfo` counts them: extended grapheme clusters (UAX #29), a
 * base with every mark, joiner and modifier that belongs to it. `e` followed by U+0301 is one, a
 * flag is two regional indicators and one element, a family emoji is five code points and one
 * element, and CR LF is one. The transpiled `StringInfo.ParseCombiningCharacters` and
 * `GetNextTextElementLength` land here, and each side asks its PLATFORM: .NET its own tables, the
 * browser `Intl.Segmenter`.
 *
 * The two agree wherever they carry the same version of the annex, and part where a newer version
 * changed a rule. .NET 10 predates the Indic conjunct rule of Unicode 15.1, so a conjunct such as
 * KA, VIRAMA, SSA is two elements to .NET and one to a current browser, and a Backspace after it
 * takes the SSA on Photon and the whole conjunct on the web. The conformance cases stay clear of
 * those rules on purpose.
 *
 * Where the browser has no segmenter (Firefox before 125), each CODE POINT is an element: a
 * surrogate pair is never split, and nothing is joined that the platform did not say to join. It
 * approximated the annex before, and got Thai, Persian, decomposed Hangul, halfwidth kana and tag
 * flags wrong in ways no test would see, since every browser the tests run in has a segmenter.
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

/** Where each text element of `text` begins: `StringInfo.ParseCombiningCharacters`. */
export function textElementStarts(text: string): number[] {
  const platform = platformSegmenter();
  if (!platform) return codePointStarts(text);
  const starts: number[] = [];
  for (const { index } of platform.segment(text)) starts.push(index);
  return starts;
}

/** Where each code point of `text` begins: the elements where the platform has no segmenter (see
 * the module's comment). Exported so its tests reach it on a platform that has one. */
export function codePointStarts(text: string): number[] {
  const starts: number[] = [];
  for (let index = 0; index < text.length; ) {
    starts.push(index);
    index += text.codePointAt(index)! > 0xffff ? 2 : 1;
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
  return rest.codePointAt(0)! > 0xffff ? 2 : 1;
}
