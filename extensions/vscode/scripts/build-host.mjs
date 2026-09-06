/**
 * Publishes the design host INTO the extension, which is what makes the extension installable.
 *
 * Without this the extension resolves `eqdesign.dll` out of the repo's own build output, so it works
 * for whoever cloned the repository and for nobody else. A `.vsix` carries the host with it.
 *
 * Framework-dependent on purpose. A self-contained publish is ~70 MB PER PLATFORM and would have to
 * ship three of them; this is a tool for people writing a .NET application, so the one dependency it
 * assumes is the one they are already using.
 */
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, rmSync, readdirSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const extension = resolve(here, '..');
const project = resolve(extension, '../../src/eQuantic.UI.Design/eQuantic.UI.Design.csproj');
const output = join(extension, 'host');

if (!existsSync(project)) {
  console.error(`build-host: ${project} is not there — this script only runs inside the repository.`);
  process.exit(1);
}

// A stale DLL from a previous shape of the host is worse than none: it loads, answers, and answers
// the way it did two releases ago.
rmSync(output, { recursive: true, force: true });

// The repository's nuget.config declares a LOCAL package source, `artifacts/packages`, and NuGet
// refuses to restore against a source that is not there (NU1301) — so a checkout without it could
// not publish the host, which is exactly what CI is. Nothing fills that directory any more (the
// Debug auto-pack that once did went in #88); it only has to EXIST, and a tracked `.gitkeep` is what
// makes a fresh clone have it. This mkdir is the belt to that pair of braces — it costs nothing and
// covers a working copy where the directory was cleaned away.
mkdirSync(resolve(extension, '../../artifacts/packages'), { recursive: true });

const result = spawnSync(
  'dotnet',
  ['publish', project, '-c', 'Release', '-o', output, '--nologo', '-v', 'quiet'],
  { stdio: 'inherit' },
);

if (result.error) {
  console.error(`build-host: could not run dotnet — ${result.error.message}`);
  process.exit(1);
}
if (result.status !== 0) {
  console.error(`build-host: dotnet publish failed (${result.status}).`);
  process.exit(result.status ?? 1);
}

const dll = join(output, 'eqdesign.dll');
if (!existsSync(dll)) {
  console.error(`build-host: publish reported success but ${dll} is not there.`);
  process.exit(1);
}

console.log(`build-host: ok (${readdirSync(output).length} files in host/)`);
