import type { FontFamily, FontFile } from './value-types';

/**
 * The faces of the families a page names, declared in the document the first time the runtime sets or
 * measures text in them (#1056): the browser's half of what the server writes in the head of a page it
 * renders (C# `FaceSink`), so a family built by hand or read from a library's catalog loads on a page the
 * browser builds as Photon loads it, the first time text asks.
 *
 * A family the document already has a face of was declared by the server, in the theme's stylesheet or
 * the page's head, whole, and is left as it is. Each file is asked for by its identity, an assembly and a
 * resource, since the browser does not know the hash of its bytes, and the server sends it on to the
 * file's content-addressed URL, so every page shares one cached copy of a file.
 */

/** The families declared, by identity: a family named twice is declared once. */
const declared = new Set<string>();

/** The family objects seen, so a family set on every render is answered without its identity. */
const seen = new WeakSet<FontFamily>();

/**
 * Declares `family`'s faces in the document, unless it ships no file, has been declared, or the document
 * already has a face of it. WHOLE or not at all, as every shell resolves a family: when one of its files
 * fails to load, every face of it is taken back, so its text falls back as a whole rather than mixing a
 * cut of it with the platform's face, and the console names the file.
 */
export function declareFamily(family: FontFamily | undefined): void {
  if (!family || family.files.length === 0 || seen.has(family)) return;
  seen.add(family);
  if (typeof document === 'undefined' || !document.fonts || typeof FontFace === 'undefined') return;
  const key = JSON.stringify([
    family.name,
    family.files.map((file) => [file.assembly, file.resource, file.minWeight, file.maxWeight, file.italic]),
  ]);
  if (declared.has(key)) return;
  declared.add(key);
  if (documentHas(family.name)) return;

  const faces = family.files.map(
    (file) =>
      new FontFace(family.name, `url("${fileUrl(file)}") format("${format(file)}")`, {
        weight: file.minWeight === file.maxWeight ? `${file.minWeight}` : `${file.minWeight} ${file.maxWeight}`,
        style: file.italic ? 'italic' : 'normal',
        display: 'swap',
      }),
  );
  let withdrawn = false;
  faces.forEach((face, index) => {
    document.fonts.add(face);
    face.loaded.catch(() => {
      if (withdrawn) return;
      withdrawn = true;
      faces.forEach((each) => document.fonts.delete(each));
      const file = family.files[index];
      console.warn(
        `The face "${family.name}" could not load ${file.resource} from ${file.assembly} ` +
          `(${fileUrl(file)}), so the family falls back whole. A page the browser builds loads a face ` +
          'an eQuantic build embedded, a font file under the Assets folder of the app or of a library.',
      );
    });
  });
}

/** Whether the document has a face of `name`, from a stylesheet or declared before. */
function documentHas(name: string): boolean {
  let found = false;
  document.fonts.forEach((face) => {
    if (face.family.replace(/^["']|["']$/g, '') === name) found = true;
  });
  return found;
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
