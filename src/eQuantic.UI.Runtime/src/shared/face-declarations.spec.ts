import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { FontFamily, FontFile } from './value-types';
import type { VisualNodeValue } from './nodes';

/** A face the document holds, as the font set reports it. */
interface Face {
  family: string;
  style: string;
  weight: string;
  status: string;
}

/** The document's font set, which a test fills and fails: the engine's own reads `@font-face` rules. */
class FakeFonts {
  readonly faces = new Set<Face>();
  private readonly failures: ((event: { fontfaces: Face[] }) => void)[] = [];

  forEach(visit: (face: Face) => void): void {
    this.faces.forEach((face) => visit(face));
  }

  addEventListener(type: string, listener: (event: { fontfaces: Face[] }) => void): void {
    if (type === 'loadingerror') this.failures.push(listener);
  }

  fail(face: Face): void {
    this.failures.forEach((listener) => listener({ fontfaces: [face] }));
  }
}

/** The `@font-face` rules of the document, by the family they declare and the file they ask for. */
function declaredRules(): string[][] {
  const rules: string[][] = [];
  for (const sheet of Array.from(document.styleSheets))
    for (const rule of Array.from(sheet.cssRules) as CSSFontFaceRule[])
      if (rule.cssText.startsWith('@font-face'))
        rules.push([rule.style.getPropertyValue('font-family').replace(/"/g, ''), rule.style.getPropertyValue('src')]);
  return rules;
}

/**
 * A family a page names outside the theme is declared in the document the first time the runtime sets or
 * measures text in it (#1056), as the server declares it in the head of a page it renders: once, left to
 * the server when the document has it, and taken back whole when a file fails to load, whoever declared
 * it, with the pages that measured in it rebuilt.
 */
describe('the faces of a family a page names', () => {
  let fonts: FakeFonts;

  beforeEach(() => {
    vi.resetModules();
    fonts = new FakeFonts();
    Object.defineProperty(document, 'fonts', { configurable: true, value: fonts });
  });

  afterEach(() => {
    delete (document as { fonts?: unknown }).fonts;
    document.querySelectorAll('style').forEach((element) => element.remove());
    vi.restoreAllMocks();
  });

  const probe = (name: string) =>
    new FontFamily(
      name,
      new FontFile('Acme.App', 'eQuantic.UI.Fonts/Probe Sans.ttf', 200, 800),
      new FontFile('Acme.App', 'eQuantic.UI.Fonts/Probe-Italic.otf', 400, 400, true),
    );

  /** A style element as the server writes the page's faces in its head. */
  const serverDeclares = (name: string) => {
    const element = document.createElement('style');
    element.id = 'eq-faces';
    element.textContent =
      `@font-face { font-family: "${name}"; src: url("/_equantic/fonts/0123456789abcdef.ttf") format("truetype"); font-weight: 400; font-style: normal; font-display: swap; }\n` +
      `@font-face { font-family: "${name}"; src: url("/_equantic/fonts/fedcba9876543210.ttf") format("truetype"); font-weight: 400; font-style: italic; font-display: swap; }`;
    document.head.appendChild(element);
  };

  it('declares each file once, as the server writes it, asked for by its identity', async () => {
    const { declareFamily } = await import('./face-declarations');

    declareFamily(probe('Probe A'));
    declareFamily(probe('Probe A'));

    expect(declaredRules()).toEqual([
      ['Probe A', 'url("/_equantic/fonts/Acme.App/eQuantic.UI.Fonts/Probe%20Sans.ttf") format("truetype")'],
      ['Probe A', 'url("/_equantic/fonts/Acme.App/eQuantic.UI.Fonts/Probe-Italic.otf") format("opentype")'],
    ]);
  });

  /** The family is a CSS string, as in the server's rules: `new FontFace` reads its family argument as a
   * CSS name in one engine and as the name itself in another, so `Roboto+Mono` was two families. */
  it('writes the family as a CSS string, so a name such as Roboto+Mono is the one the server declares', async () => {
    const { declareFamily } = await import('./face-declarations');
    const inserted = vi.spyOn(CSSStyleSheet.prototype, 'insertRule');

    declareFamily(new FontFamily('Roboto+Mono', new FontFile('Acme.App', 'eQuantic.UI.Fonts/RobotoMono.ttf', 400, 400)));

    expect(inserted.mock.calls[0][0]).toContain('font-family: "Roboto+Mono";');
  });

  it('leaves a family the document has to the declaration the server wrote', async () => {
    const { declareFamily } = await import('./face-declarations');
    fonts.faces.add({ family: '"Probe B"', style: 'normal', weight: '400', status: 'unloaded' });

    declareFamily(probe('Probe B'));

    expect(declaredRules()).toEqual([]);
  });

  it('takes a family the server declared back whole when one of its files fails, naming the file', async () => {
    const { watchFaces } = await import('./face-declarations');
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    serverDeclares('Probe C');
    watchFaces();

    fonts.fail({ family: 'Probe C', style: 'italic', weight: '400', status: 'error' });

    expect(declaredRules()).toEqual([]);
    expect(warn).toHaveBeenCalledWith(expect.stringContaining('/_equantic/fonts/fedcba9876543210.ttf'));
  });

  it('takes back a family whose file failed before the runtime was there to hear it', async () => {
    const { watchFaces } = await import('./face-declarations');
    vi.spyOn(console, 'warn').mockImplementation(() => {});
    serverDeclares('Probe D');
    fonts.faces.add({ family: 'Probe D', style: 'normal', weight: '400', status: 'error' });

    watchFaces();

    expect(declaredRules()).toEqual([]);
  });

  /** A page that measured text in a face that loaded, the upright, measures the fallback again when the
   * family is taken back over its italic: the text falls back, and a caret would sit on the face's columns. */
  it('rebuilds each page that measured in a family it takes back, once', async () => {
    const { declareFamily, dependOnFamily } = await import('./face-declarations');
    vi.spyOn(console, 'warn').mockImplementation(() => {});
    const rebuild = vi.fn();
    const family = probe('Probe E');
    declareFamily(family);
    dependOnFamily(family, rebuild);
    dependOnFamily(family, rebuild);

    fonts.fail({ family: 'Probe E', style: 'italic', weight: '400', status: 'error' });

    expect(rebuild).toHaveBeenCalledTimes(1);
    expect(declaredRules()).toEqual([]);
  });

  /** A measure in the theme's own face, a code block's columns among them, names no family: it reads the
   * stack the theme's stylesheet declares. Taken back, the theme's family falls back too, and the page
   * measures again (Copilot's second review of #1077). */
  it("rebuilds a page that measured in the theme's own face when that family is taken back", async () => {
    const { watchFaces } = await import('./face-declarations');
    const { measurePhotonText, setPhotonTheme, getPhotonTheme } = await import('./photon-context');
    const { setPassInvalidator } = await import('./pass-invalidator');
    vi.spyOn(console, 'warn').mockImplementation(() => {});
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue({
      font: '',
      measureText: (text: string) => ({ width: text.length * 7 }),
    } as unknown as CanvasRenderingContext2D);
    setPhotonTheme({ ...getPhotonTheme(), monoFamily: probe('Probe Mono') });
    watchFaces();
    const rebuild = vi.fn();

    setPassInvalidator(rebuild);
    measurePhotonText('let x = 1;', { size: 13, lineHeight: 20, weight: 'regular', tracking: 0, mono: true });
    setPassInvalidator(null);
    fonts.fail({ family: 'Probe Mono', style: 'italic', weight: '400', status: 'error' });

    expect(rebuild).toHaveBeenCalledTimes(1);
  });

  // The two places a page meets a family: the text it lowers and the text it measures, each before the
  // other may run, so each declares it.
  it('declares the family a text names when it is lowered, and when it is measured', async () => {
    const { lowerVisualNode } = await import('./lowering');
    const { measurePhotonText } = await import('./photon-context');
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue({
      font: '',
      measureText: (text: string) => ({ width: text.length * 7 }),
    } as unknown as CanvasRenderingContext2D);
    const white = { r: 255, g: 255, b: 255, a: 255 };
    const text = {
      nodeKind: 'text',
      content: 'Handgloves',
      role: 'bodyM',
      styleOverride: { size: 17, lineHeight: 22, weight: 'regular', tracking: 0, family: probe('Probe F') },
    } as unknown as VisualNodeValue;

    lowerVisualNode(text, { textPrimary: { light: white, dark: white } } as never);
    measurePhotonText('Handgloves', { size: 17, lineHeight: 22, weight: 'regular', tracking: 0, family: probe('Probe G') });

    expect(new Set(declaredRules().map(([family]) => family))).toEqual(new Set(['Probe F', 'Probe G']));
  });

  it('declares nothing for a family that ships no file', async () => {
    const { declareFamily } = await import('./face-declarations');

    declareFamily(new FontFamily('Georgia'));

    expect(declaredRules()).toEqual([]);
  });
});
