// eqc's bundle worker (#1280): one bun process a build keeps between saves, so a save pays neither for
// starting bun nor for rewriting what did not change. ModuleBundler starts it with `bun -e` and writes one
// request a line on its standard input; it answers each with one line, `@bundle ` and a JSON object.
//
// It bundles every entry point every time, with --splitting, so the chunks the entry points share stay
// the ones every page imports: a page bundled alone would carry its own copy of a shared module, and two
// copies of a module are two of its state. What it saves is everything around that: the process, and the
// files whose text is what is already there.
//
// A request: { entrypoints, root, outdir, sourcemap: "none" | "linked" | "external" }.
// An answer: { ok: true, outputs: [{ path, written }] } or { ok: false, error }. eqc composes every map
// written, whose sources are named from the folder it is written to, where the composition reads them.
import { createInterface } from "node:readline";
import { existsSync, mkdirSync, readFileSync, realpathSync, statSync, writeFileSync } from "node:fs";
import { dirname, join, relative, resolve, sep } from "node:path";

// What each map was last written from: bun's text for it, before eqc composes it with the maps of its
// sources, and each of those maps by size and time. The file on disk holds the composed map, so a map is
// written again when bun's text changed or a map it composes with did (the TypeScript of a C# file whose
// lines moved is the same text with another map), never by comparing it with the file.
const lastMaps = new Map();

// What each source of a map composes with: the map beside it, as eqc writes a TypeScript's.
function composedWith(folder, sources) {
  return sources.map(source => {
    try {
      const inner = statSync(resolve(folder, source) + ".map");
      return `${inner.size}:${inner.mtimeMs}`;
    } catch {
      return "-";
    }
  }).join("|");
}

async function bundle(request) {
  const result = await Bun.build({
    entrypoints: request.entrypoints,
    root: request.root,
    splitting: true,
    ...(request.sourcemap === "none" ? {} : { sourcemap: request.sourcemap }),
    minify: { syntax: true, whitespace: true },
    target: "browser",
    external: ["@equantic/runtime"],
    // The names `bun build` gives: an entry by its path under the root, a chunk by one of its modules.
    naming: { entry: "[dir]/[name].[ext]", chunk: "[name]-[hash].[ext]", asset: "[name]-[hash].[ext]" },
  });
  if (!result.success) return { ok: false, error: result.logs.map(String).join("\n") };

  // The folder as the file system names it: bun runs in a folder named the same way, so a source
  // named from both climbs no further than it has to (on macOS /var is /private/var).
  mkdirSync(request.outdir, { recursive: true });
  const folder = realpathSync(request.outdir);
  const outputs = [];
  for (const artifact of result.outputs) {
    const path = join(request.outdir, artifact.path);
    let text = await artifact.text();
    if (artifact.path.endsWith(".map")) {
      // In memory, bun names a map's sources from the folder it runs in; written to a folder, from
      // that folder, which is what the composition reads. Named here as bun names them there.
      const map = JSON.parse(text);
      map.sources = map.sources.map(source =>
        relative(folder, resolve(process.cwd(), source)).split(sep).join("/"));
      text = JSON.stringify(map);
      const from = text + "\n" + composedWith(folder, map.sources);
      const unchanged = lastMaps.get(path) === from && existsSync(path);
      lastMaps.set(path, from);
      if (!unchanged) write(path, text);
      outputs.push({ path, written: !unchanged });
      continue;
    }
    const unchanged = existsSync(path) && readFileSync(path, "utf8") === text;
    if (!unchanged) write(path, text);
    outputs.push({ path, written: !unchanged });
  }
  // A map this bundle did not produce is gone from the folder, so it is forgotten here too: a served
  // eqc would otherwise keep the text of every chunk map it ever wrote for the life of the server.
  const produced = new Set(outputs.map(output => output.path));
  for (const path of lastMaps.keys()) if (!produced.has(path)) lastMaps.delete(path);
  return { ok: true, outputs };
}

function write(path, text) {
  mkdirSync(dirname(path), { recursive: true });
  writeFileSync(path, text);
}

for await (const line of createInterface({ input: process.stdin })) {
  if (line.trim().length === 0) continue;
  let answer;
  try {
    answer = await bundle(JSON.parse(line));
  } catch (error) {
    answer = { ok: false, error: String(error?.stack ?? error) };
  }
  process.stdout.write("@bundle " + JSON.stringify(answer) + "\n");
}
