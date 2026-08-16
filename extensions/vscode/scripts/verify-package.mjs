/**
 * What is in the `.vsix`, and whether the thing in it runs.
 *
 * A package that builds is not a package that works. The two ways this can be quietly broken are a
 * `.vscodeignore` that leaves the design host out — the extension then installs, activates, and fails
 * on the first preview with "the design host is missing" — and a host that was published for a
 * different shape of the protocol and answers the way it did two releases ago.
 *
 * So: the entry list is read out of the archive itself (the zip central directory, no dependency),
 * and the host that was packaged is spawned and spoken to.
 */
import { spawn } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import * as readline from 'node:readline';

const here = dirname(fileURLToPath(import.meta.url));
const extension = resolve(here, '..');
const vsix = join(extension, 'equantic-ui.vsix');

if (!existsSync(vsix)) {
  console.error(`verify-package: ${vsix} is not there — run \`npm run package\`.`);
  process.exit(1);
}

/**
 * Every name in a zip, read from the central directory: locate the end-of-central-directory record
 * by its signature, then walk the entries it points at. Thirty lines, and it reads the ARCHIVE rather
 * than asking the tool that wrote it what it thinks it wrote.
 */
function entries(file) {
  const zip = readFileSync(file);
  let end = zip.length - 22;
  while (end >= 0 && zip.readUInt32LE(end) !== 0x06054b50) end--;
  if (end < 0) throw new Error('not a zip: no end-of-central-directory record');

  const count = zip.readUInt16LE(end + 10);
  let at = zip.readUInt32LE(end + 16);
  const names = [];

  for (let i = 0; i < count; i++) {
    if (zip.readUInt32LE(at) !== 0x02014b50) throw new Error(`central directory entry ${i} is malformed`);
    const nameLength = zip.readUInt16LE(at + 28);
    const extraLength = zip.readUInt16LE(at + 30);
    const commentLength = zip.readUInt16LE(at + 32);
    names.push(zip.toString('utf8', at + 46, at + 46 + nameLength));
    at += 46 + nameLength + extraLength + commentLength;
  }

  return names;
}

const names = entries(vsix);

const required = [
  'extension/package.json',
  'extension/out/extension.js',
  'extension/out/preview.js',
  'extension/host/eqdesign.dll',
  'extension/host/eqdesign.runtimeconfig.json',
  // Without it the listing shows a grey placeholder, which is a thing nobody notices until it is
  // published.
  'extension/icon.png',
  // The fallback browser runtime for NATIVE projects, which have no wwwroot of their own. Without it
  // a native project's preview fails on an instruction no amount of building can satisfy.
  'extension/runtime/runtime.js',
];

const missing = required.filter((name) => !names.includes(name));
if (missing.length > 0) {
  console.error(`verify-package: the archive is missing ${missing.join(', ')}.`);
  console.error('An extension without its host installs, activates, and fails on the first preview.');
  process.exit(1);
}

// Roslyn is what the host compiles with; without it the process starts and dies on the first request.
if (!names.some((name) => /^extension\/host\/Microsoft\.CodeAnalysis\.CSharp\.dll$/.test(name))) {
  console.error('verify-package: the host was packaged without Roslyn.');
  process.exit(1);
}

// Source has no business travelling, and `out/test` is the harness, not the product.
const unwanted = names.filter((name) => name.startsWith('extension/src/') || name.startsWith('extension/out/test/'));
if (unwanted.length > 0) {
  console.error(`verify-package: ${unwanted.length} file(s) that should not ship are in the archive, e.g. ${unwanted[0]}.`);
  process.exit(1);
}

/**
 * And now the part that matters: the host that was packaged, spoken to over its own protocol. An
 * unknown method must come back as an ERROR for the right id — which proves the process starts, reads
 * a line, parses it, dispatches it, and answers on stdout and nowhere else.
 */
const host = join(extension, 'host', 'eqdesign.dll');
const process_ = spawn('dotnet', [host], { stdio: ['pipe', 'pipe', 'pipe'] });

const answered = new Promise((resolvePromise, reject) => {
  const timer = setTimeout(() => reject(new Error('the host did not answer within 30 s')), 30_000);
  readline.createInterface({ input: process_.stdout }).on('line', (line) => {
    clearTimeout(timer);
    try {
      resolvePromise(JSON.parse(line));
    } catch {
      reject(new Error(`the host wrote something that is not a response: ${line.slice(0, 120)}`));
    }
  });
  process_.on('error', (error) => reject(new Error(`could not start the packaged host: ${error.message}`)));
});

process_.stdin.write(`${JSON.stringify({ id: 7, method: 'no-such-method', params: {} })}\n`);

let reply;
try {
  reply = await answered;
} catch (error) {
  console.error(`verify-package: ${error.message}`);
  process_.kill();
  process.exit(1);
}

process_.stdin.write(`${JSON.stringify({ id: 8, method: 'shutdown', params: {} })}\n`);

if (reply.id !== 7 || !reply.error) {
  console.error(`verify-package: the packaged host answered ${JSON.stringify(reply)}, which is not a refusal of id 7.`);
  process.exit(1);
}

const size = (readFileSync(vsix).length / (1024 * 1024)).toFixed(1);
console.log(`verify-package: ok (${names.length} files, ${size} MB, the packaged host answers)`);
