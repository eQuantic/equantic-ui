import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { FontFamily, FontFile } from './value-types';
import { declareFamily } from './face-declarations';
import { lowerVisualNode } from './lowering';
import { measurePhotonText } from './photon-context';
import type { VisualNodeValue } from './nodes';

/** A face as the document holds one: its family, its source and its descriptors, and a load that can fail. */
class FakeFace {
  readonly loaded: Promise<FakeFace>;
  fail: () => void = () => {};

  constructor(
    readonly family: string,
    readonly source: string,
    readonly descriptors: FontFaceDescriptors = {},
  ) {
    this.loaded = new Promise<FakeFace>((_, reject) => {
      this.fail = () => reject(new Error('the face failed to load'));
    });
  }
}

/**
 * A family a page names outside the theme is declared in the document the first time the runtime sets or
 * measures text in it (#1056), as the server declares it in the head of a page it renders: once, left to
 * the server when the document has it, and taken back whole when a file fails to load.
 */
describe('the faces of a family a page names', () => {
  let faces: Set<FakeFace>;

  beforeEach(() => {
    faces = new Set();
    vi.stubGlobal('FontFace', FakeFace);
    Object.defineProperty(document, 'fonts', {
      configurable: true,
      value: {
        add: (face: FakeFace) => faces.add(face),
        delete: (face: FakeFace) => faces.delete(face),
        forEach: (visit: (face: FakeFace) => void) => faces.forEach((face) => visit(face)),
      },
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    delete (document as { fonts?: unknown }).fonts;
    vi.restoreAllMocks();
  });

  const probe = (name: string) =>
    new FontFamily(
      name,
      new FontFile('Acme.App', 'eQuantic.UI.Fonts/Probe Sans.ttf', 200, 800),
      new FontFile('Acme.App', 'eQuantic.UI.Fonts/Probe-Italic.otf', 400, 400, true),
    );

  it('declares each file once, asked for by its identity', () => {
    declareFamily(probe('Probe A'));
    declareFamily(probe('Probe A'));

    expect([...faces].map((face) => [face.family, face.source, face.descriptors])).toEqual([
      [
        'Probe A',
        'url("/_equantic/fonts/Acme.App/eQuantic.UI.Fonts/Probe%20Sans.ttf") format("truetype")',
        { weight: '200 800', style: 'normal', display: 'swap' },
      ],
      [
        'Probe A',
        'url("/_equantic/fonts/Acme.App/eQuantic.UI.Fonts/Probe-Italic.otf") format("opentype")',
        { weight: '400', style: 'italic', display: 'swap' },
      ],
    ]);
  });

  it('leaves a family the document has to the declaration the server wrote', () => {
    faces.add(new FakeFace('"Probe B"', 'url("/_equantic/fonts/0123456789abcdef.ttf")'));

    declareFamily(probe('Probe B'));

    expect(faces.size).toBe(1);
  });

  it('takes a family back whole when one of its files fails to load, naming the file', async () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    declareFamily(probe('Probe C'));
    const [, italic] = [...faces];

    italic.fail();
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(faces.size).toBe(0);
    expect(warn).toHaveBeenCalledWith(expect.stringContaining('eQuantic.UI.Fonts/Probe-Italic.otf'));
  });

  // The two places a page meets a family: the text it lowers and the text it measures, each before the
  // other may run, so each declares it.
  it('declares the family a text names when it is lowered, and when it is measured', () => {
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue({
      font: '',
      measureText: (text: string) => ({ width: text.length * 7 }),
    } as unknown as CanvasRenderingContext2D);
    const white = { r: 255, g: 255, b: 255, a: 255 };
    const text = {
      nodeKind: 'text',
      content: 'Handgloves',
      role: 'bodyM',
      styleOverride: { size: 17, lineHeight: 22, weight: 'regular', tracking: 0, family: probe('Probe D') },
    } as unknown as VisualNodeValue;

    lowerVisualNode(text, { textPrimary: { light: white, dark: white } } as never);
    measurePhotonText('Handgloves', { size: 17, lineHeight: 22, weight: 'regular', tracking: 0, family: probe('Probe E') });

    expect(new Set([...faces].map((face) => face.family))).toEqual(new Set(['Probe D', 'Probe E']));
  });

  it('declares nothing for a family that ships no file', () => {
    declareFamily(new FontFamily('Georgia'));

    expect(faces.size).toBe(0);
  });
});
