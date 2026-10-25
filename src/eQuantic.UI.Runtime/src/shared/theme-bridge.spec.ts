import { describe, it, expect } from 'vitest';
import { materializeTheme, type ThemeData } from './theme-bridge';
import { photonTheme } from './design-system.generated';
import { FontFile, type AppTheme } from './value-types';
// The SAME fixture the C# ThemeBridgeTests pins (ThemeBridge.SerializeJson(PhotonTheme.Instance)).
// Round-tripping it back to the generated photonTheme proves the C# serializer and the TS materializer
// agree byte-for-byte — the guarantee that a client hydrates the exact theme the server rendered.
import photonData from './theme-bridge.photon.json';

describe('theme bridge: materializeTheme ∘ ThemeBridge.SerializeJson === photonTheme', () => {
  const data = photonData as unknown as ThemeData;
  const theme = materializeTheme(data);
  const asRecord = photonTheme as unknown as Record<string, unknown>;

  it('round-trips every surface/chrome token', () => {
    for (const key of Object.keys(data.surfaces)) {
      expect((theme as unknown as Record<string, unknown>)[key]).toEqual(asRecord[key]);
    }
    expect(theme.disabledOpacity).toBe(photonTheme.disabledOpacity);
  });

  it('round-trips every variant color group', () => {
    for (const variant of Object.keys(data.variants)) {
      expect(theme.colors(variant)).toEqual(photonTheme.colors(variant));
    }
  });

  it('round-trips the type scale', () => {
    for (const role of Object.keys(data.type)) {
      expect(theme.type(role)).toEqual(photonTheme.type(role));
    }
  });

  it('round-trips elevations 0..5', () => {
    for (let level = 0; level <= 5; level++) {
      expect(theme.elevation(level)).toEqual(photonTheme.elevation(level));
    }
  });

  it('round-trips the shape scale', () => {
    for (const scale of Object.keys(data.shape)) {
      expect(theme.shape(scale)).toBe(photonTheme.shape(scale));
    }
  });

  it('preserves the generator fallbacks (unknown variant → primary, unknown role → bodyL)', () => {
    expect(theme.colors('nope')).toEqual(theme.colors('primary'));
    expect(theme.type('nope')).toEqual(theme.type('bodyL'));
    expect(theme.shape('nope')).toBe(theme.shape('medium'));
  });
});

describe('theme bridge: a distinct theme materializes to distinct values', () => {
  it('a Material-like blob yields the M3 primary, not Photon blue', () => {
    // A hand-built blob standing in for MaterialTheme.Instance's primary (#6750A4). Only the shape of
    // the wire matters here; the C#↔TS value agreement is covered by the fixture round-trip above.
    const purple: [number, number, number, number] = [103, 80, 164, 255];
    const white: [number, number, number, number] = [255, 255, 255, 255];
    const material = materializeTheme({
      ...(photonData as unknown as ThemeData),
      variants: {
        ...(photonData as unknown as ThemeData).variants,
        primary: [
          [purple, purple],
          [white, white],
          [purple, purple],
          [purple, purple],
          [white, white],
        ],
      },
    });

    expect(material.colors('primary').base.light).toEqual({ r: 103, g: 80, b: 164, a: 255 });
    expect(material.colors('primary').base.light).not.toEqual(
      photonTheme.colors('primary').base.light,
    );
  });

  // The theme's CODE FACE, which a component may read as `context.Theme.MonoFamily`. It is the half
  // the C# side shipped first: `family` on a role crossed in the same slice, and the wire tuple
  // stopped one field short, so the client re-rendered the system font over the server's brand.
  it('carries a role whose size follows the window, as the C# ThemeBridge writes it', () => {
    const base = photonData as unknown as ThemeData;
    const fluid = materializeTheme({
      ...base,
      type: { ...base.type, display: [54, 67.5, 'bold', 0, 1.3, false, false, null, [34, 4.2, 54]] },
    } as ThemeData);
    expect(fluid.type('display').fluid?.at(1000)).toBe(42);
    expect(fluid.type('display').family).toBeUndefined();
    expect(materializeTheme(base).type('display').fluid).toBeNull();
  });

  it("carries the theme's code face, and omits it when there is none", () => {
    const base = photonData as unknown as ThemeData;
    const branded = materializeTheme({ ...base, monoFamily: 'JetBrains Mono' } as ThemeData);
    expect(branded.monoFamily?.name).toBe('JetBrains Mono');

    // Photon names none, so the payload has no such key and the client must answer undefined
    // rather than an empty string — the shape `WithThemeFace` tests for on the other side.
    expect(materializeTheme(base).monoFamily).toBeUndefined();
  });

  it("carries the sheet's slot with the members it names, and none when the theme answers none", () => {
    const data = photonData as unknown as ThemeData;
    expect(materializeTheme(data).sheet).toBeNull();
    const gridline = [
      [18, 52, 86, 1],
      [101, 67, 33, 1],
    ] as const;
    const slotted = materializeTheme({ ...data, sheet: { gridline } } as unknown as ThemeData);
    expect(slotted.sheet).not.toBeNull();
    expect(slotted.sheet!.gridline!.light).toEqual({ r: 18, g: 52, b: 86, a: 1 });
    expect(slotted.sheet!.headerText).toBeNull();
  });

  // The document's two members a theme need not answer (#1038): the text face beside the code face,
  // and the selected text's colour. The four colours every theme has ride the surfaces above.
  it("carries the theme's text face and its selected text's colour, and omits them when there are none", () => {
    const base = photonData as unknown as ThemeData;
    const white: [number, number, number, number] = [255, 255, 255, 255];
    const branded = materializeTheme({
      ...base,
      textFamily: 'Atkinson Hyperlegible Next',
      onSelection: [white, white],
    } as ThemeData);
    expect(branded.textFamily?.name).toBe('Atkinson Hyperlegible Next');
    expect(branded.onSelection?.light).toEqual({ r: 255, g: 255, b: 255, a: 255 });

    const plain = materializeTheme(base);
    expect(plain.textFamily).toBeUndefined();
    expect(plain.onSelection).toBeUndefined();
    expect(plain.selection).toEqual(photonTheme.selection);
  });

  // A family the theme ships crosses with its files (Copilot's third review of #1062): rebuilt from
  // its name alone, a component read two files on the server and none after hydration.
  it('rebuilds a shipped family with its files, for the text and code faces and for a role', () => {
    const base = photonData as unknown as ThemeData;
    const brand = [
      'Brand Sans',
      ['Acme.App', 'eQuantic.UI.Fonts/Brand.ttf', 200, 800, false],
      ['Acme.App', 'eQuantic.UI.Fonts/Brand-Italic.ttf', 200, 800, true],
    ] as const;
    const heading = base.type['heading'];
    const shipped = materializeTheme({
      ...base,
      textFamily: brand,
      monoFamily: ['Brand Mono', ['Acme.App', 'eQuantic.UI.Fonts/BrandMono.ttf', 400, 400, false]],
      type: { ...base.type, heading: [...heading.slice(0, 5), false, false, brand] },
    } as unknown as ThemeData);

    for (const family of [shipped.textFamily!, shipped.type('heading').family!]) {
      expect(family.name).toBe('Brand Sans');
      expect(family.files).toEqual([
        new FontFile('Acme.App', 'eQuantic.UI.Fonts/Brand.ttf', 200, 800, false),
        new FontFile('Acme.App', 'eQuantic.UI.Fonts/Brand-Italic.ttf', 200, 800, true),
      ]);
      expect(family.files[1]).toBeInstanceOf(FontFile);
    }
    expect(shipped.monoFamily!.files).toEqual([new FontFile('Acme.App', 'eQuantic.UI.Fonts/BrandMono.ttf', 400, 400, false)]);
    expect(materializeTheme({ ...base, textFamily: 'Georgia' } as ThemeData).textFamily!.files).toEqual([]);
  });

  it('materializeTheme returns a structurally valid AppTheme', () => {
    const t: AppTheme = materializeTheme(photonData as unknown as ThemeData);
    expect(typeof t.colors).toBe('function');
    expect(typeof t.type).toBe('function');
    expect(typeof t.elevation).toBe('function');
    expect(typeof t.shape).toBe('function');
    expect(t.surface.light).toBeDefined();
  });
});
