/**
 * The nonce the page was served with: the one the server stamps on every script and style it writes
 * (#1266), read off the first element that carries it. A browser hides the attribute's value once
 * the page is parsed, so a selector cannot read it, and only the element's `nonce` property can.
 * Empty on a page served without one.
 */
export function pageNonce(): string {
  const carrier = document.querySelector<HTMLElement>('script[nonce],style[nonce]');
  return carrier?.nonce ?? '';
}

/**
 * A `<style>` element the runtime adds to the head, carrying the page's nonce (#1268). Under a
 * strict Content-Security-Policy a style element without it is refused, and the browser gives it
 * no sheet, so every rule inserted into it is lost. The nonce is set before the element enters the
 * document, which is when the browser decides.
 */
export function createStyleElement(id: string): HTMLStyleElement {
  const element = document.createElement('style');
  element.id = id;
  const nonce = pageNonce();
  if (nonce) element.nonce = nonce;
  document.head.appendChild(element);
  return element;
}
