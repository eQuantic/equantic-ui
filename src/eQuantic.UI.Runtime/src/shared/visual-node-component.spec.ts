import { effectiveStyle } from './style-atomizer';
import { afterEach, describe, expect, it } from 'vitest';
import { Card } from './__transpiled__/Card';
import { photonTheme } from './design-system.generated';
import { getPhotonTheme, setPhotonTheme } from './photon-context';
import { ColorToken } from './value-types';
import { Text } from './vocabulary';
import { VisualNodeComponent } from './visual-node-component';

afterEach(() => setPhotonTheme(photonTheme));

describe('VisualNodeComponent (the Core⇄Shared client bridge)', () => {
  it('renders an abstract subtree with the ambient theme — the SSR adapter parity pair', () => {
    const adapter = new VisualNodeComponent(new Card(new Text('body'), 'filled'));
    const node = adapter.render();

    expect(node.tag).toBe('div');
    expect(effectiveStyle(node)).toContain('border-radius: 14px');
    expect(effectiveStyle(node)).toContain('background-color: light-dark(#eff1f4, #1c232b)');
  });

  it('boot-time theme registration: setPhotonTheme swaps what the ambient lowering resolves', () => {
    const magenta = new ColorToken({ r: 255, g: 0, b: 255, a: 255 });
    setPhotonTheme({ ...photonTheme, textPrimary: magenta });
    expect(getPhotonTheme().textPrimary).toBe(magenta);

    // A Text with no explicit color inherits the REGISTERED theme's textPrimary.
    const node = new VisualNodeComponent(new Text('hello')).render();
    expect(effectiveStyle(node)).toContain('color: #ff00ff');
  });

  it('an explicit theme argument overrides the ambient one (the C# optional parameter)', () => {
    const custom = { ...photonTheme, textPrimary: new ColorToken({ r: 0, g: 128, b: 0, a: 255 }) };
    const node = new VisualNodeComponent(new Text('hello'), custom).render();
    expect(effectiveStyle(node)).toContain('color: #008000');
  });
});
