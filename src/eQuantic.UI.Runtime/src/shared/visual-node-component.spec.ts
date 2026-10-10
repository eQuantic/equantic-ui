import { effectiveStyle } from './style-atomizer';
import { afterEach, describe, expect, it } from 'vitest';
import { Card } from './components/Card';
import { TextInput } from './components/TextInput';
import { photonTheme } from './design-system.generated';
import { getPhotonTheme, setPhotonDensity, setPhotonTheme } from './photon-context';
import { ColorToken } from './value-types';
import { Text } from './vocabulary';
import type { HtmlElement, HtmlNode } from '../core/types';
import { VisualNodeComponent } from './visual-node-component';

afterEach(() => {
  setPhotonTheme(photonTheme);
  setPhotonDensity('comfortable');
});

/** Every pixel height the lowered tree declares, the node and all its descendants. */
function heights(node: HtmlNode): string[] {
  const own = effectiveStyle(node).match(/(?<![-\w])height: \d+px/g) ?? [];
  return [...own, ...node.children.flatMap(heights)];
}

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

  // The C# is `VisualNodeComponent : HtmlElement`, so a Core page may hold one wherever an
  // HtmlElement fits and eqc emits that annotation. This case is checked by TSC rather than by the
  // expectation below it — the annotation IS the assertion, and the expectation only keeps vitest
  // from reporting an empty test. While the DOM surface sat on `Component` the twins agreed by
  // accident; the move down one class (#245) broke it, measured:
  //
  //   TS2739: Type 'VisualNodeComponent' is missing the following properties from type
  //           'HtmlElement': buildAttributes, buildEvents, htmlNode
  //
  // Mutation: put `extends Component` back on the mirror and this file stops type-checking.
  it('is an HtmlElement, because the C# type is one', () => {
    const asElement: HtmlElement = new VisualNodeComponent(new Text('hello'));
    expect(asElement.render().tag).toBe('span');
  });

  it('an explicit theme argument overrides the ambient one (the C# optional parameter)', () => {
    const custom = { ...photonTheme, textPrimary: new ColorToken({ r: 0, g: 128, b: 0, a: 255 }) };
    const node = new VisualNodeComponent(new Text('hello'), custom).render();
    expect(effectiveStyle(node)).toContain('color: #008000');
  });

  // A bridge given its OWN theme is still a part of this page, so what it composes lowers at the
  // page's density, as the C# twin builds every bridge at the request's (#623). A stateful component
  // expands with the bridge's own context, which was a bare `{ theme, typeScale }` with no density:
  // a large TextInput drew its Comfortable 48px field in a Compact page, where the server drew 40px
  // (found by Copilot on #688). The bridge without a theme, which reads the runtime's context, is
  // the control.
  it.each([
    ['without a theme', undefined],
    ['with an explicit theme', photonTheme],
  ])('lowers a stateful component %s at the page density, as the server does', (_, theme) => {
    setPhotonDensity('compact');
    const node = new VisualNodeComponent(new TextInput(''), theme).render();
    expect(heights(node)).toContain('height: 40px');
    expect(heights(node)).not.toContain('height: 48px');
  });
});
