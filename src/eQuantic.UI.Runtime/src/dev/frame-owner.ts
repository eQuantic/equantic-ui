/**
 * Names each frame of a remapped stack by WHOSE code it ran and where, in the developer's terms
 * (#1108). A developer using eQuantic.UI never reads JavaScript, so no frame is named by a `.js`
 * file's line and column, mapped or not: the app's code is its C#, the SDK's is the SDK's own C#
 * where eqc transpiled it into the runtime, and the runtime's module elsewhere. Flutter names a
 * framework frame the same way, by its package path in the framework's own language.
 */

import type { RemappedFrame } from './stack-remapper';

/** The app's code, the SDK's, or neither (a browser extension, a script the page did not write). */
export type FrameOwner = 'app' | 'sdk' | 'external';

export interface NamedFrame {
  owner: FrameOwner;
  /** The member the frame ran, as the stack names it, or null for an anonymous one. */
  func: string | null;
  /** Where it ran, as the developer reads it: a C# file, the runtime's module, an app module, a host. */
  place: string;
  /** The line in {@link place}, when the map led there; null where it names no file's line. */
  line: number | null;
  /** True when {@link place} is a C# file. */
  csharp: boolean;
  /** The C# text the map carried, for the source context: the app's, never the SDK's. */
  sourceContent: string | null;
}

/** The name the overlay gives the SDK's runtime, a module of it after `›`. */
export const RUNTIME_NAME = 'eQuantic.UI runtime';

const withoutQuery = (url: string): string => url.split(/[?#]/)[0];

/** Every frame named by its owner. `runtimeUrl` is the served runtime's own URL (the overlay ships in
 * it, so `import.meta.url`), and `pageOrigin` the page's: a module under /_equantic/ there is the app's. */
export function nameFrames(
  frames: RemappedFrame[],
  runtimeUrl: string,
  pageOrigin: string,
): NamedFrame[] {
  const runtime = withoutQuery(runtimeUrl);
  return frames.map((frame) => {
    const url = withoutQuery(frame.generatedUrl);
    if (url === runtime) return sdkFrame(frame);
    const location = parse(url);
    if (location && location.origin === pageOrigin && location.pathname.startsWith('/_equantic/')) {
      return appFrame(frame, location.pathname);
    }
    return {
      owner: 'external',
      func: frame.func,
      // By its host alone: a script the page did not write is neither the app's nor the SDK's, and its
      // file is JavaScript the developer never wrote either.
      place: location?.host || 'the browser',
      line: null,
      csharp: false,
      sourceContent: null,
    };
  });
}

function sdkFrame(frame: RemappedFrame): NamedFrame {
  const named = { owner: 'sdk' as const, func: frame.func, sourceContent: null };
  if (!frame.mapped) return { ...named, place: RUNTIME_NAME, line: null, csharp: false };
  // The runtime's map names each source from src/: the SDK's C# where eqc transpiled the module, the
  // runtime's TypeScript elsewhere, and the SDK's boot.
  if (frame.file.endsWith('.cs'))
    return {
      ...named,
      func: memberOf(frame.func, frame.file),
      place: frame.file,
      line: frame.line,
      csharp: true,
    };
  return {
    ...named,
    place: `${RUNTIME_NAME} › ${runtimeModule(frame.file)}`,
    line: frame.line,
    csharp: false,
  };
}

function appFrame(frame: RemappedFrame, pathname: string): NamedFrame {
  const named = { owner: 'app' as const, func: frame.func };
  if (frame.mapped && frame.file.endsWith('.cs')) {
    return {
      ...named,
      func: memberOf(frame.func, frame.file),
      // An app module's map names its sources from the map's folder (`../../Screens/Home.cs`): the
      // developer reads the project's own path.
      place: frame.file.replace(/^(\.\.\/)+/, ''),
      line: frame.line,
      csharp: true,
      sourceContent: frame.sourceContent,
    };
  }
  // A map that stopped in eqc's TypeScript, or none at all: the module, which is the C# type it was
  // written from, and no position, since the only one there is is JavaScript's.
  const name = frame.mapped ? baseName(frame.file).replace(/\.ts$/, '') : moduleOf(pathname);
  return { ...named, place: name, line: null, csharp: false, sourceContent: null };
}

/** The member a frame led to C# ran, with its type named as the C# file names it: bun renames a class
 * whose name another module of the bundle also declares (`SheetController3.moveTo`), and the developer
 * wrote `SheetController`. */
function memberOf(func: string | null, csFile: string): string | null {
  const type = baseName(csFile).replace(/\.cs$/, '');
  // Each name in the frame compared with the type, rather than a pattern built from a file's name.
  return (
    func &&
    func.replace(/[A-Za-z_$][\w$]*/g, (name) =>
      name.length > type.length && name.startsWith(type) && /^\d+$/.test(name.slice(type.length))
        ? type
        : name,
    )
  );
}

/** `eQuantic.UI.Runtime/src/shared/lowering.ts` → `shared/lowering`; the SDK's boot → `boot`. A module eqc
 * transpiled that carries no map of its own (a record's, which eqc writes on one line with no
 * mapping) is the C# type it was written from: `shared/components/Owner$Inner.ts` → `Owner.Inner`. */
function runtimeModule(source: string): string {
  const transpiled = /(?:^|\/)eQuantic\.UI\.Runtime\/src\/shared\/components\/([^/]+)\.ts$/.exec(
    source,
  );
  if (transpiled) return transpiled[1].replace(/\$/g, '.');
  const inRuntime = /(?:^|\/)eQuantic\.UI\.Runtime\/src\/(.+)\.ts$/.exec(source);
  if (inRuntime) return inRuntime[1];
  return baseName(source).replace(/\.[jt]s$/, '');
}

/** `/_equantic/Dashboard.js` → `Dashboard`, and a chunk bun named with a hash (`NotFoundScreen-a3bmepcm.js`)
 * by the module it is named after. */
function moduleOf(pathname: string): string {
  return baseName(pathname)
    .replace(/\.js$/, '')
    .replace(/-[a-z0-9]{8}$/, '');
}

const baseName = (path: string): string => path.split('/').pop() ?? path;

function parse(url: string): URL | null {
  try {
    return new URL(url);
  } catch {
    return null;
  }
}

/** The frame as a line of the call stack: the member, then where it ran, at its C# line. The report,
 * which the SDK's maintainers read, also gives a runtime module's line. */
export function describeFrame(frame: NamedFrame, forReport = false): string {
  const at = frame.line !== null && (frame.csharp || forReport);
  return `${frame.func ?? '<anonymous>'} — ${at ? `${frame.place}:${frame.line}` : frame.place}`;
}
