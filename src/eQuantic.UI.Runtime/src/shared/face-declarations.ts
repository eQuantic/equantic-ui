import type { FontFamily, FontFile } from './value-types';

/**
 * The faces of the families a page names, declared in the document the first time the runtime sets or
 * measures text in them (#1056): the browser's half of what the server writes in the head of a page it
 * renders (C# `FaceSink`), so a family built by hand or read from a library's catalog loads on a page the
 * browser builds as Photon loads it, the first time text asks.
 *
 * Declared as the server declares them, `@font-face` rules in the `eq-faces` style element, with the
 * family's name as a CSS string, which every browser reads alike: `new FontFace` reads its family
 * argument as a CSS family name in one engine and as the name itself in another, so a name such as
 * `Roboto+Mono` or a quoted one was a different family depending on the browser. A family the document
 * already has a face of was declared by the server, in the theme's stylesheet or the page's head, and is
 * left as it is. Each file is asked for by its identity, an assembly and a resource, since the browser
 * does not know the hash of its bytes, and the server sends it on to the file's content-addressed URL.
 */

/** The element the document's faces are declared in: the server's head element (C# `FaceSink.ElementId`), or one added here. */
const ELEMENT_ID = 'eq-faces';

/** The families declared, by identity: a family named twice is declared once. */
const declared = new Set<string>();

/** The family objects seen, so a family set on every render is answered without its identity. */
const seen = new WeakSet<FontFamily>();

/** The families taken back: a family a file of which failed is not declared again in this document. */
const withdrawn = new Set<string>();

/**
 * The pages that measured text in each family, so they rebuild if it is taken back. Held weakly: a page
 * that left the tree is no one's to rebuild, and its invalidator goes with it.
 */
const dependents = new Map<string, { pages: Set<WeakRef<() => void>>; known: WeakSet<() => void> }>();

/**
 * Declares `family`'s faces in the document, unless it ships no file, has been declared or taken back,
 * or the document already has a face of it.
 */
export function declareFamily(family: FontFamily | undefined): void {
  if (!family || family.files.length === 0 || seen.has(family)) return;
  seen.add(family);
  if (!watchFaces()) return;
  const key = JSON.stringify([
    family.name,
    family.files.map((file) => [file.assembly, file.resource, file.minWeight, file.maxWeight, file.italic]),
  ]);
  if (declared.has(key) || withdrawn.has(family.name)) return;
  declared.add(key);
  if (documentHas(family.name)) return;
  const sheet = facesSheet();
  if (!sheet) return;
  for (const file of family.files) sheet.insertRule(faceRule(family.name, file), sheet.cssRules.length);
}

/**
 * Records that the page `rebuild` belongs to measured text in `family`, so it rebuilds should the family
 * be taken back after it measured: the text falls back, and a measure taken in the face would place a
 * caret beside the character it is on.
 */
export function dependOnFamily(family: FontFamily | undefined, rebuild: (() => void) | null): void {
  if (!family || family.files.length === 0 || !rebuild || typeof WeakRef === 'undefined') return;
  let entry = dependents.get(family.name);
  if (!entry) dependents.set(family.name, (entry = { pages: new Set(), known: new WeakSet() }));
  if (entry.known.has(rebuild)) return;
  entry.known.add(rebuild);
  for (const page of entry.pages) if (!page.deref()) entry.pages.delete(page);
  entry.pages.add(new WeakRef(rebuild));
}

let listening = false;

/**
 * Watches the document's faces, once, answering whether it can hold any: a face that fails to load, the
 * theme's, the server's or one declared here, takes its whole family back, as every shell resolves a
 * family whole, rather than leaving a cut of it beside the platform's face. A face that failed before the
 * runtime was there to hear it is taken back now. The boot calls it, and so does every declaration.
 */
export function watchFaces(): boolean {
  if (typeof document === 'undefined' || !document.fonts) return false;
  if (!listening) {
    listening = true;
    document.fonts.addEventListener('loadingerror', (event) => {
      for (const face of (event as FontFaceSetLoadEvent).fontfaces) withdraw(unquoted(face.family), face);
    });
    const failed: FontFace[] = [];
    document.fonts.forEach((face) => {
      if (face.status === 'error') failed.push(face);
    });
    for (const face of failed) withdraw(unquoted(face.family), face);
  }
  return true;
}

/**
 * Takes `name` back whole: every `@font-face` rule of it, in every stylesheet the document can read, so
 * its text falls back, and the console names the file that failed. The pages that measured text in it
 * rebuild, to measure the fallback they now draw.
 */
function withdraw(name: string, failed: FontFace): void {
  if (withdrawn.has(name)) return;
  withdrawn.add(name);
  let source = '';
  for (const sheet of Array.from(document.styleSheets)) {
    let rules: CSSRuleList;
    try {
      rules = sheet.cssRules;
    } catch {
      continue;
    }
    for (let index = rules.length - 1; index >= 0; index--) {
      const rule = rules[index] as CSSFontFaceRule;
      if (!rule.cssText.startsWith('@font-face') || unquoted(rule.style.getPropertyValue('font-family')) !== name) continue;
      if (rule.style.getPropertyValue('font-style') === failed.style && rule.style.getPropertyValue('font-weight') === failed.weight)
        source = rule.style.getPropertyValue('src');
      sheet.deleteRule(index);
    }
  }
  console.warn(
    `The face "${name}" could not load ${source || 'one of its files'}, so the family falls back whole. ` +
      'The web serves a face the theme names, or one an eQuantic build embedded: a font file under the ' +
      'Assets folder of the app or of a library it references.',
  );
  const entry = dependents.get(name);
  dependents.delete(name);
  entry?.pages.forEach((page) => page.deref()?.());
}

/** Whether the document has a face of `name`, from a stylesheet or declared before. */
function documentHas(name: string): boolean {
  let found = false;
  document.fonts.forEach((face) => {
    if (unquoted(face.family) === name) found = true;
  });
  return found;
}

/** The sheet of the `eq-faces` element, added to the head when the server wrote none. */
function facesSheet(): CSSStyleSheet | null {
  let element = document.getElementById(ELEMENT_ID) as HTMLStyleElement | null;
  if (!element) {
    element = document.createElement('style');
    element.id = ELEMENT_ID;
    document.head.appendChild(element);
  }
  return element.sheet;
}

/** The rule of one file, as the server writes one (C# `FontFaceRules.Write`), asked for by its identity. */
function faceRule(name: string, file: FontFile): string {
  const weight = file.minWeight === file.maxWeight ? `${file.minWeight}` : `${file.minWeight} ${file.maxWeight}`;
  return (
    `@font-face { font-family: "${name}"; src: url("${fileUrl(file)}") format("${format(file)}"); ` +
    `font-weight: ${weight}; font-style: ${file.italic ? 'italic' : 'normal'}; font-display: swap; }`
  );
}

/** A family name without the quotes a browser writes around it. */
function unquoted(family: string): string {
  return family.replace(/^["']|["']$/g, '');
}

/** Where a file is asked for by its identity: `/_equantic/fonts/<assembly>/<resource>`. */
function fileUrl(file: FontFile): string {
  return (
    `/_equantic/fonts/${encodeURIComponent(file.assembly)}/` +
    file.resource.split('/').map(encodeURIComponent).join('/')
  );
}

/** The format a source names: `opentype` for CFF, `truetype` otherwise (C# `FontFaceRules.Format`). */
function format(file: FontFile): string {
  return /\.otf$/i.test(file.resource) ? 'opentype' : 'truetype';
}
