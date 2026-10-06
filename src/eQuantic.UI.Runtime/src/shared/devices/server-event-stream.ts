import type { BindOutcome, ServerEventsListener, ServerEventsTransport } from './server-events';

/** Where the server serves the stream: `ServerEventsEndpoints.Path`. */
const PATH = '/_equantic/events';

/** `EventSource.CLOSED`, spelled here so that nothing reads the global before a stream is opened. */
const CLOSED = 2;

/** How long to wait before opening a stream the browser gave up on, the first time and at most. */
const FIRST_RETRY_MS = 1_000;
const LAST_RETRY_MS = 30_000;

type OpenSource = (url: string) => EventSource;
type Request = (url: string, init: RequestInit) => Promise<Response>;

/** A request's answer, read whole. */
interface Answer {
  readonly status: number;
  readonly text: string;
}

/**
 * The page's connection to the server's events over Server-Sent Events: one `EventSource`, the
 * browser's own client, which the server opens with a `connection` event naming the connection's id
 * and feeds with a `message` event per payload; a topic is bound and released by a request naming
 * that id.
 *
 * A stream that drops is opened again by the browser itself, which sends the last event's id. A
 * stream the browser gives up on (an answer that is not a stream, a proxy's error page) is opened
 * again here, waiting longer after each failure in a row, until the server hands out a connection.
 */
export class ServerEventStream implements ServerEventsTransport {
  private source: EventSource | null = null;
  private listener: ServerEventsListener | null = null;
  private retry: ReturnType<typeof setTimeout> | null = null;
  private failures = 0;

  constructor(
    private readonly openSource: OpenSource = (url) => new EventSource(url),
    private readonly request: Request = (url, init) => fetch(url, init),
  ) {}

  open(listener: ServerEventsListener): void {
    this.listener = listener;
    this.connect();
  }

  close(): void {
    this.listener = null;
    if (this.retry !== null) clearTimeout(this.retry);
    this.retry = null;
    this.source?.close();
    this.source = null;
    this.failures = 0;
  }

  async bind(connection: string, topic: string): Promise<BindOutcome> {
    let answer: Answer;
    try {
      answer = await this.post(connection, 'subscribe', topic);
    } catch {
      return 'failed';
    }
    if (answer.status === 204) return 'bound';
    if (answer.status === 404) return 'gone';
    if (answer.status !== 403) return 'failed';
    // A 403 the server's own refusal did not write (a proxy's, a firewall's) is still a refusal.
    const reason = (read(answer.text) as { reason?: unknown } | undefined)?.reason;
    return reason === 'unknown' || reason === 'limitReached' ? reason : 'forbidden';
  }

  async release(connection: string, topic: string): Promise<void> {
    try {
      await this.post(connection, 'release', topic);
    } catch {
      // The server releases every topic of a connection that ends, and a request that cannot reach
      // it means the connection is ending.
    }
  }

  /**
   * Makes the request and reads its answer whole, an empty one included: the browser reports a
   * response nobody read as aborted, and a network panel full of failed subscriptions that worked
   * sends whoever looks there after the wrong problem.
   */
  private async post(
    connection: string,
    action: 'subscribe' | 'release',
    topic: string,
  ): Promise<Answer> {
    const response = await this.request(`${PATH}/${encodeURIComponent(connection)}/${action}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ topic }),
      credentials: 'same-origin',
    });
    return { status: response.status, text: await response.text() };
  }

  private connect(): void {
    const source = this.openSource(PATH);
    this.source = source;
    // Each handler first asks whether its stream is still the one: a replaced or closed stream's
    // late events report nothing.
    source.addEventListener('connection', (event) => {
      if (this.source !== source) return;
      const id = (read((event as MessageEvent).data) as { id?: unknown } | undefined)?.id;
      if (typeof id !== 'string') return;
      this.failures = 0;
      this.listener?.connected(id);
    });
    source.addEventListener('message', (event) => {
      if (this.source !== source) return;
      const message = event as MessageEvent;
      const data = read(message.data) as { topic?: unknown; payload?: unknown } | undefined;
      if (typeof data?.topic !== 'string') return;
      this.listener?.message(data.topic, data.payload, message.lastEventId || null);
    });
    source.addEventListener('error', () => {
      if (this.source !== source) return;
      this.listener?.dropped();
      if (source.readyState !== CLOSED) return;
      source.close();
      const delay = Math.min(FIRST_RETRY_MS * 2 ** this.failures, LAST_RETRY_MS);
      this.failures++;
      this.retry = setTimeout(() => {
        this.retry = null;
        if (this.listener !== null) this.connect();
      }, delay);
    });
  }
}

/** An event's data as JSON, or undefined when it is not JSON. */
function read(data: unknown): unknown {
  if (typeof data !== 'string') return undefined;
  try {
    return JSON.parse(data);
  } catch {
    return undefined;
  }
}
