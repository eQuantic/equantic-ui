/**
 * Whether the browser's router and the server chose the same page for a URL (#1778).
 *
 * The server draws the page its own routing resolves, after whatever the app's middleware did to the
 * request (a rewrite of `/` to the catalogue, say), and says which in the document and in a
 * navigation's payload. The browser's router resolves the URL from the route table alone. When the two
 * differ, a navigation the router answers shows its page with the other page's data, and nothing says
 * so: a real site's home page drew empty texts after a language change. In development the browser
 * says it, naming both.
 */

/** Where the docs explain why the two must agree, and how to serve one page at another's path. */
export const PAGE_AGREEMENT_DOCS =
  'https://github.com/eQuantic/equantic-ui/wiki/ServerIntegration#the-router-and-the-server-choose-the-same-page';

/**
 * Whether `routed`, the page the router chose for `path`, is another page than `served`, the one the
 * server drew; in development, says so. A router that chose nothing (a path no route declares, which
 * the server answered with its not-found page) disagrees with nothing.
 */
export function routerDisagrees(path: string, served: string | null | undefined, routed: string | null | undefined, dev: boolean): boolean {
  if (!served || !routed || served === routed) return false;
  if (dev) {
    console.error(
      `[eQuantic.UI] the server drew ${served}, the router chose ${routed} for ${path}. A navigation to ` +
        `${path} shows ${routed} with what the server loads for ${served}. Serve the page the route ` +
        `declares, rather than rewriting the path on the server: ${PAGE_AGREEMENT_DOCS}`,
    );
  }
  return true;
}
