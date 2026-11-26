import { afterEach, describe, expect, it } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { Pagination } from './components/Pagination';
import { photonTheme } from './design-system.generated';
import { lowerVisualNode } from './lowering';
import { measurePhotonText, setPhotonTheme } from './photon-context';
import { installCulture } from '../utils/culture';
import type { HtmlNode } from '../core/types';

/**
 * The browser's pager speaks the reader's language (#1874): the transpiled `Pagination` reads its steps and
 * its pages' names through `$eq.str` from the culture's catalog, as the server reads them from the
 * `ResourceManager` (`PaginationTests`). The catalog here holds the Brazilian Portuguese the SDK ships,
 * read from its resx, so a literal written back into the twin fails.
 */
function shipped(key: string): string {
  const resx = readFileSync(
    resolve(__dirname, '../../../eQuantic.UI.Components/SdkResources.pt-BR.resx'),
    'utf8',
  );
  const match = new RegExp(
    `<data name="${key}" xml:space="preserve">\\s*<value>([^<]*)</value>`,
  ).exec(resx);
  if (match === null) throw new Error(`SdkResources.pt-BR.resx has no ${key}`);
  return match[1];
}

function walk(node: HtmlNode): HtmlNode[] {
  return [node, ...node.children.flatMap(walk)];
}

function text(node: HtmlNode): string {
  return node.textContent ?? node.children.map(text).join('');
}

describe('Pagination in the browser speaks the UI culture (#1874)', () => {
  afterEach(() => installCulture('en-US', 'en-US', {}));

  it('reads its steps and its pages’ names from the culture’s catalog', () => {
    const keys = ['PreviousPage', 'NextPage', 'PageNumbered'];
    installCulture(
      'pt-BR',
      'pt-BR',
      Object.fromEntries(keys.map((key) => [`SdkResources/${key}`, shipped(key)])),
    );
    setPhotonTheme(photonTheme);
    const tree = lowerVisualNode(new Pagination(5, 2) as never, {
      textPrimary: photonTheme.textPrimary,
      componentContext: { theme: photonTheme, typeScale: 1, measureText: measurePhotonText },
    });

    const steps = walk(tree)
      .filter((node) => node.tag === 'button')
      .map(text)
      .filter((label) => label.length > 0 && !/^\d+$/.test(label));
    expect(steps).toEqual([shipped('PreviousPage'), shipped('NextPage')]);
    expect(walk(tree).map((node) => node.attributes['aria-label'])).toContain(
      shipped('PageNumbered').replace('{0}', '2'),
    );
    expect(steps).toEqual(['Anterior', 'Próxima']);
  });
});
