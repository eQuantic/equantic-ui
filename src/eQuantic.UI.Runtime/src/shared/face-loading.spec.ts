import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { FontFamily } from './value-types';

/**
 * A face the theme ships is declared by the server and fetched by the browser when text first uses
 * it, and until then a canvas measures that text as its fallback (#1055). The text is drawn in the
 * face once it arrives, so a page that measured, a code editor placing its caret on columns, must
 * measure again: it rebuilds when the face has loaded.
 */
describe('a page that measured text in a face still loading', () => {
  let loaded: (font: string) => boolean;
  let loads: number;
  let asked: string[];
  let finishLoading: () => void;
  let failLoading: () => void;

  beforeEach(() => {
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue({
      font: '',
      measureText: (text: string) => ({ width: text.length * 7 }),
    } as unknown as CanvasRenderingContext2D);
    loaded = () => false;
    loads = 0;
    asked = [];
    Object.defineProperty(document, 'fonts', {
      configurable: true,
      value: {
        check: (font: string) => loaded(font),
        load: (font: string) => {
          loads++;
          asked.push(font);
          return new Promise<void>((resolve, reject) => {
            finishLoading = () => {
              loaded = () => true;
              resolve();
            };
            failLoading = () => reject(new Error('the face failed to load'));
          });
        },
      },
    });
  });

  afterEach(async () => {
    delete (document as { fonts?: unknown }).fonts;
    (await import('./pass-invalidator')).setPassInvalidator(null);
    vi.restoreAllMocks();
  });

  const brand = { size: 17, lineHeight: 22, weight: 'regular', tracking: 0, family: new FontFamily('Brand Sans') };

  it('rebuilds once the face has loaded, however many times it measured', async () => {
    const { measurePhotonText } = await import('./photon-context');
    const { setPassInvalidator } = await import('./pass-invalidator');
    const rebuild = vi.fn();

    setPassInvalidator(rebuild);
    measurePhotonText('Handgloves', brand);
    measurePhotonText('Handgloves again', brand);
    setPassInvalidator(null);

    expect(rebuild).not.toHaveBeenCalled();
    expect(loads).toBe(1);
    finishLoading();
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(rebuild).toHaveBeenCalledTimes(1);
  });

  it('rebuilds nothing for a face that is there', async () => {
    const { measurePhotonText } = await import('./photon-context');
    const { setPassInvalidator } = await import('./pass-invalidator');
    const rebuild = vi.fn();
    loaded = () => true;

    setPassInvalidator(rebuild);
    measurePhotonText('Handgloves', brand);
    setPassInvalidator(null);
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(loads).toBe(0);
    expect(rebuild).not.toHaveBeenCalled();
  });

  it('rebuilds nothing when the face fails to load, and asks for it no more', async () => {
    const { measurePhotonText } = await import('./photon-context');
    const { setPassInvalidator } = await import('./pass-invalidator');
    const rebuild = vi.fn();
    const broken = { ...brand, family: new FontFamily('Broken Sans') };

    setPassInvalidator(rebuild);
    measurePhotonText('Handgloves', broken);
    failLoading();
    await new Promise((resolve) => setTimeout(resolve, 0));
    measurePhotonText('Handgloves', broken);
    setPassInvalidator(null);

    expect(rebuild).not.toHaveBeenCalled();
    expect(loads).toBe(1);
  });

  /** The italic cut is a file of its own, and the upright one being there says nothing of it: the
   * font asked for names the slant, or a page measuring italic text never rebuilt. */
  it('waits for the italic cut when the upright one is there already', async () => {
    const { measurePhotonText } = await import('./photon-context');
    const { setPassInvalidator } = await import('./pass-invalidator');
    const rebuild = vi.fn();
    loaded = (font) => !font.startsWith('italic ');

    setPassInvalidator(rebuild);
    measurePhotonText('Handgloves', { ...brand, italic: true });
    setPassInvalidator(null);

    expect(asked).toEqual([expect.stringMatching(/^italic 400 17px "Brand Sans", /)]);
    finishLoading();
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(rebuild).toHaveBeenCalledTimes(1);
  });

  it('waits for nothing outside a build', async () => {
    const { measurePhotonText } = await import('./photon-context');

    measurePhotonText('Handgloves', brand);

    expect(loads).toBe(0);
  });
});
