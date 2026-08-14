/**
 * The webview's script is a STRING inside the extension, so `tsc` checks the string and not the
 * JavaScript in it. A stray backtick, an unescaped `${`, a typo in a handler — all compile cleanly
 * and fail only when the panel is opened, as a blank frame.
 *
 * This extracts the document the panel builds and parses the script out of it, which is the one
 * check that would have caught it. Runs after every compile.
 */
import { readFileSync } from 'node:fs';

const source = readFileSync(new URL('../out/preview.js', import.meta.url), 'utf8');

const marker = source.indexOf('/* html */ `');
if (marker < 0) {
  console.error('verify-webview: no HTML template found in out/preview.js — has the panel changed shape?');
  process.exit(1);
}

// Walk to the backtick that closes the template, stepping over escapes and ${…} interpolations.
const open = source.indexOf('`', marker);
let end = open + 1;
let depth = 0;
for (; end < source.length; end++) {
  if (source[end] === '\\') { end++; continue; }
  if (source[end] === '`' && depth === 0) break;
  if (source[end] === '$' && source[end + 1] === '{') { depth++; end++; continue; }
  if (source[end] === '}' && depth > 0) depth--;
}

const template = source.slice(open + 1, end);
const html = new Function('nonce', 'csp', `return \`${template}\``)('NONCE', 'CSP');

const script = html.match(/<script nonce="NONCE">([\s\S]*?)<\/script>/);
if (!script) {
  console.error('verify-webview: the nonced script block is missing from the document.');
  process.exit(1);
}

try {
  // eslint-disable-next-line no-new-func
  new Function(script[1]);
} catch (error) {
  console.error(`verify-webview: the webview script does not parse — ${error.message}`);
  console.error('This would open as a blank preview with nothing to explain it.');
  process.exit(1);
}

/**
 * Parsing is not enough. A template literal EATS escapes — `\n` becomes a newline and `\d` becomes
 * a plain `d` — so a regex written into the panel can arrive semantically different while still
 * parsing perfectly. `/^-?\d+$/` silently becoming `/^-?d+$/` matches nothing and nobody notices.
 * These are the literals whose meaning depends on a backslash surviving.
 */
const survives = [
  [String.raw`/^-?\d+(\.\d+)?[fFdDmM]?$/`, 'the number-literal test'],
  [String.raw`/^"(?:[^"\\]|\\.)*"$/`, 'the string-literal test'],
  [String.raw`/"@equantic\/runtime"/g`, 'the runtime specifier the compiled module is pointed at'],
  [String.raw`'\n\n'`, 'the blank line in a reported error'],
];

const eaten = survives.filter(([needle]) => !script[1].includes(needle));
if (eaten.length > 0) {
  for (const [needle, what] of eaten) {
    console.error(`verify-webview: ${what} lost its backslash — expected ${needle} in the emitted script.`);
  }
  console.error('A template literal consumes escapes; write them doubled in the TypeScript source.');
  process.exit(1);
}

console.log(`verify-webview: ok (${script[1].length} chars of script, ${html.length} of document)`);
