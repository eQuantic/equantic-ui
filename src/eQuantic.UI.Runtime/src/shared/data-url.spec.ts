/**
 * The twin of the C# `DataUrlEncodingTests` (#1937): the fixture the C# table wrote, replayed through
 * the twin's own table, so a tile's data URL and a raster's CSS string are the same bytes from SSR
 * and from the browser. Regenerate the fixture with EQ_UPDATE_DATA_URL_FIXTURE=1 on that test.
 */

import { describe, expect, it } from 'vitest';
import fixture from './data-url.fixture.json';
import { cssUrl, dataUrlText } from './tile-artwork';

describe('the data URL table (C# DataUrlEncodingTests cross-pin)', () => {
  it('encodes every case of the fixture as the C# table does', () => {
    expect(fixture.encode.length).toBeGreaterThan(128);
    for (const [text, encoded] of fixture.encode) expect(dataUrlText(text), JSON.stringify(text)).toBe(encoded);
  });

  it('writes every source of the fixture as the C# CSS string', () => {
    expect(fixture.css.length).toBeGreaterThan(128);
    for (const [text, written] of fixture.css) expect(cssUrl(text), JSON.stringify(text)).toBe(written);
  });

  // A raw control character or brace in a source was read as the sheet's structure when the sheet's
  // text was read again (security review W1 and W2, #1937): both are hex escapes inside the string.
  it('writes every control character and brace of a source as a hex escape', () => {
    expect(cssUrl('/a.png\u0001x')).toBe('url("/a.png\\1 x")');
    expect(cssUrl('a\u007fb')).toBe('url("a\\7F b")');
    expect(cssUrl('/a.png?.eq-1x2y3z{')).toBe('url("/a.png?.eq-1x2y3z\\7B ")');
    expect(cssUrl('a}b')).toBe('url("a\\7D b")');
  });

  it('leaves what encodeURIComponent would not, and escapes what a data URL needs', () => {
    expect(dataUrlText(" '()*!~")).toBe(" '()*!~");
    expect(dataUrlText('é#')).toBe('%C3%A9%23');
  });
});
