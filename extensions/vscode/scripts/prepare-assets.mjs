/**
 * Copies the Marketplace icon in from the repository's own assets, and refuses one that would be
 * rejected.
 *
 * `vsce` only packages what is inside the extension folder, so the icon has to be copied rather than
 * referenced — and a copy is a thing that drifts. This ran once by hand and the copy was a stale
 * 100x100 within the same minute the source became 128x128, which is the whole argument for doing it
 * on every package.
 *
 * The Marketplace requires PNG, at least 128x128, and prohibits SVG. That rule is checked HERE, where
 * it can still stop the build, rather than by an upload that fails after a tag has been pushed.
 */
import { copyFileSync, existsSync, readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const extension = resolve(here, '..');
const source = resolve(extension, '../../assets/equantic-vscode-icon.png');
const target = join(extension, 'icon.png');

if (!existsSync(source)) {
  console.error(`prepare-assets: ${source} is not there.`);
  process.exit(1);
}

const bytes = readFileSync(source);

// The signature, then IHDR's width and height — which is all a PNG has to say for this.
if (!bytes.subarray(0, 8).equals(Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]))) {
  console.error('prepare-assets: the icon is not a PNG. The Marketplace prohibits SVG and accepts PNG.');
  process.exit(1);
}

const width = bytes.readUInt32BE(16);
const height = bytes.readUInt32BE(20);

if (width < 128 || height < 128) {
  console.error(`prepare-assets: the icon is ${width}x${height}; the Marketplace requires at least 128x128.`);
  console.error('Re-export it rather than upscaling — a listing icon is permanent-ish and soft edges show.');
  process.exit(1);
}

copyFileSync(source, target);
console.log(`prepare-assets: icon ok (${width}x${height}, ${bytes.length} bytes)`);
