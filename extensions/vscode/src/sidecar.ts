import { ChildProcessWithoutNullStreams, spawn } from 'child_process';
import * as readline from 'readline';

/** One thing the compiler had to say, anchored to the span that caused it. Zero-based, like the editor's own model. */
export interface DesignMark {
  startLine: number;
  startColumn: number;
  endLine: number;
  endColumn: number;
  message: string;
  code: string;
  isError: boolean;
}

export interface InitializeResult {
  assemblyName: string;
  sourceFiles: number;
  references: number;
  elapsedMs: number;
}

/**
 * What may be DONE with a selected node, which is not the same question as where it came from.
 * `literal` is written unconditionally in Build; `derived` is inside a loop, a conditional or a
 * callback, so it has no separate existence to move or delete; `foreign` is written in another
 * member or another file.
 */
export interface OriginTier {
  tier: 'literal' | 'derived' | 'foreign';
  reason: string;
  member: string | null;
}

/** One row of the inspector: what a property is, what this call says for it, and whether the form
 * the call is written in can reach it at all. */
export interface NodeProperty {
  name: string;
  type: string;
  kind: 'argument' | 'initializer' | 'unset';
  value: string | null;
  editable: boolean;
  reason: string | null;
  options: string[] | null;
  summary: string | null;
  /** The members of the VALUE written here, when it is written as a construction — a BoxStyle's
   * padding, background and radius. Each is an ordinary property in its own right. */
  members?: NodeProperty[] | null;
}

/** One list a call is written with — children, but also a Grid's columns or a menu's items. */
export interface NodeList {
  name: string;
  elementType: string;
  count: number;
  /** Whether its elements are nodes on the canvas, or data only the panel can reach. */
  visual: boolean;
  /** Each entry as written — data lists only; a visual one's elements are whole subtrees. */
  entries: string[] | null;
}

export interface InspectResult {
  component: string;
  form: 'factory' | 'new' | 'unknown';
  summary: string | null;
  properties: NodeProperty[];
  /** How many children the declarative list holds, or -1 when there is no such list. */
  childCount: number;
  /** Position among the parent's declarative children, or -1 when this node is not an element of
   * such a list — which is what decides whether it can be moved or removed. */
  siblingIndex: number;
  siblingCount: number;
  /** Why this container cannot take an insertion, when it cannot. */
  insertReason: string | null;
  /** Every list this call is written with, children included. */
  lists: NodeList[] | null;
}

/** One entry a palette can offer, with the smallest call of it that compiles. */
export interface PaletteEntry {
  name: string;
  snippet: string;
  summary: string | null;
  /** Where it comes from: the app's own generated surface, the framework's, or — for a list of data
   * rather than of nodes — the element type's own ways of being written. */
  source: 'app' | 'framework' | 'value';
}

/** One text replacement to apply, or the reason there is none. Computed by the host; applied by the
 * editor, so it lands in the document's own undo stack. */
export interface EditResult {
  applied: boolean;
  reason: string | null;
  startLine: number;
  startColumn: number;
  endLine: number;
  endColumn: number;
  newText: string;
  /** Where the node ENDED UP, for the edits that move it. An origin is a span, so the one the caller
   * was holding now names whatever slid into those coordinates. */
  origin?: string | null;
}

/** One file as the editor currently has it, saved or not. */
export interface OpenBuffer {
  path: string;
  text: string;
}

export interface CompileResult {
  success: boolean;
  js: string;
  className: string;
  marks: DesignMark[];
  elapsedMs: number;
}

interface Pending {
  resolve: (value: unknown) => void;
  reject: (reason: Error) => void;
}

/**
 * The design host, as seen from the editor: one long-lived .NET process holding the project's Roslyn
 * compilation, spoken to in newline-delimited JSON over stdio.
 *
 * Stdio rather than a port: nothing to choose, nothing to authorise, nothing left listening if the
 * window dies, and it works unchanged over Remote-SSH because the extension host runs on the remote
 * machine anyway. The host's own stdout is the protocol and NOTHING else — its logging goes to
 * stderr, which lands in the output channel.
 */
export class Sidecar {
  private process: ChildProcessWithoutNullStreams | undefined;
  private readonly pending = new Map<number, Pending>();
  private nextId = 1;
  private exited = false;

  constructor(
    private readonly dllPath: string,
    private readonly log: (line: string) => void,
  ) {}

  start(): void {
    if (this.process) return;

    this.process = spawn('dotnet', [this.dllPath], { stdio: ['pipe', 'pipe', 'pipe'] });
    this.exited = false;

    readline.createInterface({ input: this.process.stdout }).on('line', (line) => this.receive(line));
    this.process.stderr.on('data', (chunk: Buffer) => this.log(chunk.toString().trimEnd()));

    this.process.on('exit', (code) => {
      this.exited = true;
      // Anything still waiting will never be answered — fail it rather than leave the preview
      // spinning on a promise nobody will settle.
      const reason = new Error(`the design host exited (code ${code ?? 'unknown'})`);
      for (const pending of this.pending.values()) pending.reject(reason);
      this.pending.clear();
      this.process = undefined;
    });

    this.process.on('error', (error) => this.log(`failed to start the design host: ${error.message}`));
  }

  private receive(line: string): void {
    let message: { id: number; result?: unknown; error?: string };
    try {
      message = JSON.parse(line);
    } catch {
      // Prose on the protocol stream: report it rather than desynchronising silently.
      this.log(`unparseable line from the design host: ${line.slice(0, 200)}`);
      return;
    }

    const pending = this.pending.get(message.id);
    if (!pending) return;
    this.pending.delete(message.id);

    if (message.error) pending.reject(new Error(message.error));
    else pending.resolve(message.result);
  }

  private send<T>(method: string, params: Record<string, unknown>): Promise<T> {
    if (!this.process || this.exited) return Promise.reject(new Error('the design host is not running'));

    const id = this.nextId++;
    const promise = new Promise<T>((resolve, reject) => {
      this.pending.set(id, { resolve: resolve as (value: unknown) => void, reject });
    });
    this.process.stdin.write(`${JSON.stringify({ id, method, params })}\n`);
    return promise;
  }

  initialize(projectDir: string, refsFile: string, generatedDir: string): Promise<InitializeResult> {
    return this.send('initialize', { projectDir, refsFile, generatedDir });
  }

  diagnose(path: string, text: string, buffers: OpenBuffer[]): Promise<DesignMark[]> {
    return this.send('diagnose', { path, text, buffers });
  }

  compile(path: string, text: string, buffers: OpenBuffer[]): Promise<CompileResult> {
    return this.send('compile', { path, text, buffers });
  }

  classify(path: string, text: string, origin: string, buffers: OpenBuffer[]): Promise<OriginTier> {
    return this.send('classify', { path, text, origin, buffers });
  }

  /** Empty string rather than null when the origin names nothing inspectable — the host answers
   * every request, and a missing reply would leave the panel waiting forever. */
  inspect(path: string, text: string, origin: string, buffers: OpenBuffer[]): Promise<InspectResult | ''> {
    return this.send('inspect', { path, text, origin, buffers });
  }

  /** Takes a member back out of a value's initializer — `style.Padding`, unset. */
  unsetProperty(
    path: string, text: string, origin: string, property: string, buffers: OpenBuffer[],
  ): Promise<EditResult> {
    return this.send('unsetProperty', { path, text, origin, property, buffers });
  }

  setProperty(
    path: string, text: string, origin: string, property: string, value: string, buffers: OpenBuffer[],
  ): Promise<EditResult> {
    return this.send('setProperty', { path, text, origin, property, value, buffers });
  }

  /** Type-directed when it is told which list it is filling: a Grid's `columns` is offered GridTracks,
   * not components. Without a list it answers with the component surface. */
  palette(path?: string, text?: string, origin?: string, list?: string): Promise<PaletteEntry[]> {
    return this.send('palette', { path, text, origin, list });
  }

  insertChild(
    path: string, text: string, origin: string, index: number, snippet: string, buffers: OpenBuffer[],
    list?: string,
  ): Promise<EditResult> {
    return this.send('insertChild', { path, text, origin, index, snippet, buffers, list });
  }

  /** Removes the entry at a position of a named list. By POSITION because the things in a data list
   * never render, so nothing on the canvas carries their span. */
  removeAt(
    path: string, text: string, origin: string, list: string, index: number, buffers: OpenBuffer[],
  ): Promise<EditResult> {
    return this.send('removeAt', { path, text, origin, list, index, buffers });
  }

  moveChild(path: string, text: string, origin: string, delta: number, buffers: OpenBuffer[]): Promise<EditResult> {
    return this.send('moveChild', { path, text, origin, delta, buffers });
  }

  /** A node leaving its own container for another list, both in the same file. */
  moveAcross(
    path: string, text: string, origin: string, target: string, index: number, buffers: OpenBuffer[],
  ): Promise<EditResult> {
    return this.send('moveAcross', { path, text, origin, target, index, buffers });
  }

  /** Where a drag ends: `index` is the position the node ends up at in the finished list, not the gap
   * it was dropped into. */
  reorderChild(path: string, text: string, origin: string, index: number, buffers: OpenBuffer[]): Promise<EditResult> {
    return this.send('reorderChild', { path, text, origin, index, buffers });
  }

  removeChild(path: string, text: string, origin: string, buffers: OpenBuffer[]): Promise<EditResult> {
    return this.send('removeChild', { path, text, origin, buffers });
  }

  theme(): Promise<string> {
    return this.send('theme', {});
  }

  dispose(): void {
    if (!this.process) return;
    try {
      this.process.stdin.write(`${JSON.stringify({ id: this.nextId++, method: 'shutdown', params: {} })}\n`);
    } catch {
      // Already gone; the kill below is the backstop.
    }
    this.process.kill();
    this.process = undefined;
  }
}
