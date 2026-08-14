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

  diagnose(path: string, text: string): Promise<DesignMark[]> {
    return this.send('diagnose', { path, text });
  }

  compile(path: string, text: string): Promise<CompileResult> {
    return this.send('compile', { path, text });
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
