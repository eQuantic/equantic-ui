import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { Decimal } from '../../utils/decimal';
import { equals } from '../../utils/equals';
import { hydrate } from '../../utils/hydrate';
import { ServerConnection, ServerTopic, ServerTopicRefusal } from '../primitive-values';
import { ServerEventStream } from './server-event-stream';
import { WebServerEvents } from './server-events';

/**
 * The browser's half of IServerEvents (#291), driven through its real Server-Sent Events transport:
 * the `EventSource` and the requests are fakes that answer as the server's endpoints answer, so what
 * is under test is everything the page does with them.
 */

/** An `EventSource` the test speaks for: it emits what the server would write. */
class FakeSource {
  readyState = 0;
  closed = false;
  private readonly handlers = new Map<string, ((event: unknown) => void)[]>();

  constructor(readonly url: string) {
    sources.push(this);
  }

  addEventListener(type: string, handler: (event: unknown) => void): void {
    this.handlers.set(type, [...(this.handlers.get(type) ?? []), handler]);
  }

  close(): void {
    this.closed = true;
    this.readyState = 2;
  }

  /** The server's first event: the connection's id. */
  connect(id: string): void {
    this.readyState = 1;
    this.emit('connection', { id });
  }

  publish(topic: string, payload: unknown, id = ''): void {
    this.emit('message', { topic, payload }, id);
  }

  /** The stream drops: the browser opens it again by itself (0), or it gave up on it (2). */
  fail(readyState: 0 | 2): void {
    this.readyState = readyState;
    for (const handler of this.handlers.get('error') ?? []) handler({ type: 'error' });
  }

  private emit(type: string, data: unknown, lastEventId = ''): void {
    // The browser keeps the last id it was given, and an event without one reports it again.
    if (lastEventId !== '') this.lastEventId = lastEventId;
    const event = { type, data: JSON.stringify(data), lastEventId: this.lastEventId };
    for (const handler of this.handlers.get(type) ?? []) handler(event);
  }

  private lastEventId = '';
}

interface Sent {
  readonly url: string;
  readonly topic: string;
  readonly answer: (status: number, body?: unknown) => void;
  /** Whether the page read the answer's body: one left unread shows as aborted in the network panel. */
  read: boolean;
}

let sources: FakeSource[];
let sent: Sent[];
/** Answers every request as soon as it is made, unless a test takes them over. */
let respond: ((request: Sent) => void) | null;

function events(): WebServerEvents {
  const transport = new ServerEventStream(
    (url) => new FakeSource(url) as unknown as EventSource,
    (url, init) =>
      new Promise<Response>((resolve) => {
        const request: Sent = {
          url,
          topic: (JSON.parse(init.body as string) as { topic: string }).topic,
          answer: (status, body) =>
            resolve({
              status,
              text: async () => {
                request.read = true;
                return body === undefined ? '' : JSON.stringify(body);
              },
            } as unknown as Response),
          read: false,
        };
        sent.push(request);
        respond?.(request);
      }),
  );
  return new WebServerEvents(transport);
}

/** Lets every request answered and every task queued run. */
const settle = () => new Promise((resolve) => setTimeout(resolve, 0));

/** A record's twin, as eqc writes one: its members revived by their own specs. */
class Quote {
  declare readonly symbol: string;
  declare readonly price: Decimal;
  static readonly $hydration = { price: 'decimal' } as const;
}

const prices = new ServerTopic('prices', Quote);
const room = (id: string) => new ServerTopic(`room:${id}`, null);

describe('WebServerEvents', () => {
  beforeEach(() => {
    sources = [];
    sent = [];
    respond = (request) => request.answer(204);
  });
  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it('opens one stream for every topic, with the first subscription, and binds each once it has its id', async () => {
    const page = events();
    page.subscribe(prices, () => {});
    page.subscribe(room('a'), () => {});
    await settle();

    expect(sources).toHaveLength(1);
    expect(sources[0].url).toBe('/_equantic/events');
    expect(sent).toEqual([]);

    sources[0].connect('c1');
    await settle();
    expect(sent.map((request) => [request.url, request.topic])).toEqual([
      ['/_equantic/events/c1/subscribe', 'prices'],
      ['/_equantic/events/c1/subscribe', 'room:a'],
    ]);
  });

  it('revives each payload as the C# type its topic carries', async () => {
    const page = events();
    const heard: unknown[] = [];
    page.subscribe(prices, (quote) => heard.push(quote));
    page.subscribe(new ServerTopic('rate', 'decimal'), (rate) => heard.push(rate));
    sources[0].connect('c1');
    await settle();

    sources[0].publish('prices', { symbol: 'EQ', price: '10.50' });
    sources[0].publish('rate', '0.1');

    expect(heard[0]).toBeInstanceOf(Quote);
    expect((heard[0] as Quote).price).toBeInstanceOf(Decimal);
    expect(String((heard[0] as Quote).price)).toBe('10.50');
    expect(heard[1]).toBeInstanceOf(Decimal);
  });

  it('shares one binding among the subscriptions to a topic, and each hears every payload', async () => {
    const page = events();
    const heard: string[] = [];
    page.subscribe(room('a'), (text) => heard.push(`first ${text}`));
    page.subscribe(room('a'), (text) => heard.push(`second ${text}`));
    sources[0].connect('c1');
    await settle();

    sources[0].publish('room:a', 'hello');
    sources[0].publish('room:b', 'not held');

    expect(sent.map((request) => request.topic)).toEqual(['room:a']);
    expect(heard).toEqual(['first hello', 'second hello']);
  });

  it('reads every answer whole, an empty one included', async () => {
    respond = (request) =>
      request.topic === 'room:b' ? request.answer(403, { reason: 'unknown' }) : request.answer(204);
    const page = events();
    const refusals: ServerTopicRefusal[] = [];
    page.subscribe(prices, () => {});
    const leaving = page.subscribe(room('a'), () => {});
    page.subscribe(
      room('b'),
      () => {},
      (refusal) => refusals.push(refusal),
    );
    sources[0].connect('c1');
    await settle();
    leaving.dispose();
    await settle();

    expect(sent.map((request) => request.url.slice(request.url.lastIndexOf('/') + 1))).toEqual([
      'subscribe',
      'subscribe',
      'subscribe',
      'release',
    ]);
    expect(sent.filter((request) => !request.read)).toEqual([]);
    expect(refusals).toEqual([new ServerTopicRefusal('room:b', 'unknown')]);
  });

  it('reports a refusal with why, after which the topic hears nothing', async () => {
    respond = (request) =>
      request.topic === 'room:b'
        ? request.answer(403, { reason: 'forbidden' })
        : request.answer(204);
    const page = events();
    const refusals: ServerTopicRefusal[] = [];
    const heard: unknown[] = [];
    page.subscribe(room('a'), () => {});
    page.subscribe(
      room('b'),
      (text) => heard.push(text),
      (refusal) => refusals.push(refusal),
    );
    sources[0].connect('c1');
    await settle();

    sources[0].publish('room:b', 'too late');

    expect(refusals).toEqual([new ServerTopicRefusal('room:b', 'forbidden')]);
    expect(heard).toEqual([]);
  });

  it('closes the stream when the last subscription goes, leaving the release to the stream’s end', async () => {
    const page = events();
    const states: string[] = [];
    page.onConnectionChanged((connection) => states.push(connection.state));
    const prices$ = page.subscribe(prices, () => {});
    const room$ = page.subscribe(room('a'), () => {});
    sources[0].connect('c1');
    await settle();

    room$.dispose();
    room$.dispose();
    await settle();
    expect(sent.map((request) => request.url)).toContain('/_equantic/events/c1/release');
    expect(sources[0].closed).toBe(false);

    const before = sent.length;
    prices$.dispose();
    await settle();
    expect(sources[0].closed).toBe(true);
    expect(sent).toHaveLength(before);
    expect(states).toEqual(['connecting', 'connected', 'disconnected']);
    expect(page.connection).toEqual(ServerConnection.disconnected);
  });

  it('keeps the stream when one subscription goes and the next arrives in the same task', async () => {
    const page = events();
    const leaving = page.subscribe(room('a'), () => {});
    sources[0].connect('c1');
    await settle();

    leaving.dispose();
    page.subscribe(room('a'), () => {});
    await settle();

    expect(sources).toHaveLength(1);
    expect(sources[0].closed).toBe(false);
    expect(sent.map((request) => request.topic)).toEqual(['room:a']);
  });

  it('releases by request a topic it left to the stream’s end, when a later subscription keeps the stream open', async () => {
    const page = events();
    const leaving = page.subscribe(room('a'), () => {});
    sources[0].connect('c1');
    await settle();

    leaving.dispose();
    // Later in the task: the release was already left to the stream's end when this arrives.
    for (let turn = 0; turn < 5; turn++) await Promise.resolve();
    page.subscribe(room('b'), () => {});
    await settle();

    expect(sources[0].closed).toBe(false);
    expect(sent.map((request) => [request.url, request.topic])).toContainEqual([
      '/_equantic/events/c1/release',
      'room:a',
    ]);
  });

  it('binds every live topic again on the connection that replaces a dropped one, then reports it with the last id', async () => {
    const page = events();
    const readings: ServerConnection[] = [];
    page.onConnectionChanged((connection) => readings.push(connection));
    page.subscribe(prices, () => {});
    page.subscribe(room('a'), () => {});
    sources[0].connect('c1');
    await settle();
    sources[0].publish('room:a', 'seventh', '7');

    sources[0].fail(0);
    expect(page.connection).toEqual(new ServerConnection('reconnecting', '7'));
    respond = null;
    sources[0].connect('c2');
    await settle();
    const rebinding = sent.filter((request) => request.url.startsWith('/_equantic/events/c2/'));
    expect(rebinding.map((request) => request.topic)).toEqual(['prices', 'room:a']);
    expect(page.connection.state).toBe('reconnecting');

    rebinding.forEach((request) => request.answer(204));
    await settle();
    expect(readings).toEqual([
      new ServerConnection('connecting', null),
      new ServerConnection('connected', null),
      new ServerConnection('reconnecting', '7'),
      new ServerConnection('connected', '7'),
    ]);
  });

  it('opens again a stream the browser gave up on, waiting longer after each failure in a row', async () => {
    vi.useFakeTimers();
    const page = events();
    page.subscribe(room('a'), () => {});

    sources[0].fail(2);
    expect(page.connection.state).toBe('reconnecting');
    await vi.advanceTimersByTimeAsync(999);
    expect(sources).toHaveLength(1);
    await vi.advanceTimersByTimeAsync(1);
    expect(sources).toHaveLength(2);

    sources[1].fail(2);
    await vi.advanceTimersByTimeAsync(1_999);
    expect(sources).toHaveLength(2);
    await vi.advanceTimersByTimeAsync(1);
    expect(sources).toHaveLength(3);

    sources[2].connect('c3');
    await vi.waitFor(() => expect(page.connection.state).toBe('connected'));
    sources[2].fail(2);
    await vi.advanceTimersByTimeAsync(1_000);
    expect(sources).toHaveLength(4);
  });

  it('never has two requests for one topic on the wire', async () => {
    respond = null;
    const page = events();
    page.subscribe(prices, () => {});
    sources[0].connect('c1');
    await settle();
    sent[0].answer(204);

    const first = page.subscribe(room('a'), () => {});
    await settle();
    first.dispose();
    page.subscribe(room('a'), () => {});
    await settle();
    expect(sent.map((request) => request.topic)).toEqual(['prices', 'room:a']);

    sent[1].answer(204);
    await settle();
    expect(sent).toHaveLength(2);

    // Released while its bind was on the wire: the release waits for the bind's answer, and a new
    // subscription waits for the release's.
    const second = page.subscribe(room('b'), () => {});
    await settle();
    second.dispose();
    await settle();
    expect(sent.slice(2).map((request) => request.url)).toEqual(['/_equantic/events/c1/subscribe']);
    sent[2].answer(204);
    await settle();
    expect(sent.slice(3).map((request) => request.url)).toEqual(['/_equantic/events/c1/release']);
    page.subscribe(room('b'), () => {});
    await settle();
    expect(sent).toHaveLength(4);
    sent[3].answer(204);
    await settle();
    expect(sent.slice(4).map((request) => request.url)).toEqual(['/_equantic/events/c1/subscribe']);
  });

  it('binds on the new connection a topic whose request reached the one it replaced', async () => {
    respond = null;
    const page = events();
    const refusals: ServerTopicRefusal[] = [];
    page.subscribe(
      room('a'),
      () => {},
      (refusal) => refusals.push(refusal),
    );
    sources[0].connect('c1');
    await settle();

    sources[0].fail(0);
    sources[0].connect('c2');
    sent[0].answer(404);
    await settle();

    expect(sent.map((request) => request.url)).toEqual([
      '/_equantic/events/c1/subscribe',
      '/_equantic/events/c2/subscribe',
    ]);
    expect(refusals).toEqual([]);
  });

  it('fails, and says why, a bind the server keeps not knowing the connection of while the stream stands', async () => {
    vi.useFakeTimers();
    const error = vi.spyOn(console, 'error').mockImplementation(() => {});
    respond = (request) => request.answer(404);
    const page = events();
    const refusals: ServerTopicRefusal[] = [];
    page.subscribe(
      room('a'),
      () => {},
      (refusal) => refusals.push(refusal),
    );
    sources[0].connect('c1');
    await vi.advanceTimersByTimeAsync(0);
    expect(refusals).toEqual([]);

    // Asked again after half a second, a second and two: four answers in a row before it is told.
    await vi.advanceTimersByTimeAsync(3_500);

    expect(sent.map((request) => request.url)).toEqual(
      Array(4).fill('/_equantic/events/c1/subscribe'),
    );
    expect(refusals).toEqual([new ServerTopicRefusal('room:a', 'failed')]);
    expect(error.mock.calls[0][0]).toContain('session affinity');
  });

  it('binds on the next connection a topic whose bind met the end of its stream', async () => {
    vi.useFakeTimers();
    respond = null;
    const page = events();
    const refusals: ServerTopicRefusal[] = [];
    page.subscribe(
      room('a'),
      () => {},
      (refusal) => refusals.push(refusal),
    );
    sources[0].connect('c1');
    await vi.advanceTimersByTimeAsync(0);

    // The server ended the stream while it authorized the bind, and its 404 arrives before the
    // browser sees the stream end.
    sent[0].answer(404);
    await vi.advanceTimersByTimeAsync(0);
    sources[0].fail(0);
    sources[0].connect('c2');
    await vi.advanceTimersByTimeAsync(500);
    sent[1].answer(204);
    await vi.advanceTimersByTimeAsync(0);

    expect(sent.map((request) => request.url)).toEqual([
      '/_equantic/events/c1/subscribe',
      '/_equantic/events/c2/subscribe',
    ]);
    expect(refusals).toEqual([]);
    expect(page.connection.state).toBe('connected');
  });

  it('asks again a bind the server could not answer, and binds it', async () => {
    vi.useFakeTimers();
    const answers = [503, 204];
    respond = (request) => request.answer(answers.shift()!);
    const page = events();
    const refusals: ServerTopicRefusal[] = [];
    page.subscribe(
      room('a'),
      () => {},
      (refusal) => refusals.push(refusal),
    );
    sources[0].connect('c1');
    await vi.advanceTimersByTimeAsync(500);

    expect(sent.map((request) => request.url)).toEqual([
      '/_equantic/events/c1/subscribe',
      '/_equantic/events/c1/subscribe',
    ]);
    expect(refusals).toEqual([]);
    expect(page.connection.state).toBe('connected');
  });

  it('refuses at once, and says why, a topic of a page whose server serves no events', async () => {
    const error = vi.spyOn(console, 'error').mockImplementation(() => {});
    window.__EQ_CONFIG = { serverEvents: false };
    try {
      const page = events();
      const refusals: ServerTopicRefusal[] = [];
      page.subscribe(
        prices,
        () => {},
        (refusal) => refusals.push(refusal),
      );
      await settle();

      expect(sources).toEqual([]);
      expect(refusals).toEqual([new ServerTopicRefusal('prices', 'unknown')]);
      expect(error.mock.calls[0][0]).toContain('UseServerEvents');
      expect(page.connection.state).toBe('disconnected');
    } finally {
      delete window.__EQ_CONFIG;
    }
  });

  it('revives the payloads of a topic that crossed the wire, as the one built here does', async () => {
    // A Server Action's result, as the server writes it and eqc's spec rebuilds it.
    const crossed = hydrate(
      { name: 'prices' },
      { of: ServerTopic, members: {}, typeArguments: [Quote] },
    ) as ServerTopic;
    expect(crossed).toBeInstanceOf(ServerTopic);
    expect(equals(crossed, prices)).toBe(true);

    const page = events();
    const heard: unknown[] = [];
    page.subscribe(crossed, (quote) => heard.push(quote));
    sources[0].connect('c1');
    await settle();
    sources[0].publish('prices', { symbol: 'EQ', price: '0.1' });

    expect(heard[0]).toBeInstanceOf(Quote);
    expect((heard[0] as Quote).price).toBeInstanceOf(Decimal);
  });

  it('keeps delivering to the others when one subscriber throws', async () => {
    const error = vi.spyOn(console, 'error').mockImplementation(() => {});
    const page = events();
    const heard: string[] = [];
    page.subscribe(room('a'), () => {
      throw new Error('broken subscriber');
    });
    page.subscribe(room('a'), (text) => heard.push(text as string));
    sources[0].connect('c1');
    await settle();

    sources[0].publish('room:a', 'still arrives');

    expect(heard).toEqual(['still arrives']);
    expect(error).toHaveBeenCalledOnce();
  });
});

describe('ServerTopic', () => {
  it('refuses an empty or a missing name, as the C# constructor does', () => {
    expect(() => new ServerTopic('')).toThrow(
      "The value cannot be an empty string. (Parameter 'name')",
    );
    expect(() => new ServerTopic(null as unknown as string)).toThrow(
      "Value cannot be null. (Parameter 'name')",
    );
  });

  it('equals another of the same name and payload type', () => {
    expect(equals(new ServerTopic('ids', ['long']), new ServerTopic('ids', ['long']))).toBe(true);
    expect(equals(new ServerTopic('ids', ['long']), new ServerTopic('other', ['long']))).toBe(
      false,
    );
  });
});
