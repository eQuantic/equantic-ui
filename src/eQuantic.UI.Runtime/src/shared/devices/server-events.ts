import { hydrate } from '../../utils/hydrate';
import type { ServerConnectionStateValue, ServerTopicRefusalReasonValue } from '../enums.generated';
import { ServerConnection, ServerTopicRefusal, type ServerTopic } from '../primitive-values';
import { ServerEventStream } from './server-event-stream';

/**
 * The way the page reaches the server's events. Server-Sent Events is the one there is
 * ({@link ServerEventStream}); the subscriptions, the revival and the reconnection's bookkeeping
 * live in {@link WebServerEvents} and talk to the server only through this, so a WebSocket would be
 * one more implementation rather than a rewrite.
 */
export interface ServerEventsTransport {
  /** Opens the connection and keeps it open, opening it again after every drop, until `close`. */
  open(listener: ServerEventsListener): void;
  /** Closes the connection; nothing is reported after it. */
  close(): void;
  /** Asks the server to bind a topic to a connection. Never rejects. */
  bind(connection: string, topic: string): Promise<BindOutcome>;
  /** Asks the server to release a topic from a connection. Never rejects. */
  release(connection: string, topic: string): Promise<void>;
}

/** What a transport reports. */
export interface ServerEventsListener {
  /** A connection is open, under the id the server issued it: a new one after every drop. */
  connected(connection: string): void;
  /** An event: its topic, its payload as JSON parsed it, and its id when the server gave it one. */
  message(topic: string, payload: unknown, id: string | null): void;
  /** The connection dropped, and the transport is opening it again. */
  dropped(): void;
}

/**
 * The server's answer to a bind: bound, refused and why, or `gone` when the server does not know the
 * connection, because the one it knew has ended and the page's next connection binds the topic.
 */
export type BindOutcome = 'bound' | 'gone' | ServerTopicRefusalReasonValue;

interface Subscription {
  dispose(): void;
}

/** One `Subscribe` call: its own topic, because two subscriptions to one name may revive differently. */
interface Subscriber {
  readonly topic: ServerTopic;
  readonly onEvent: (payload: unknown) => void;
  readonly onRefused: ((refusal: ServerTopicRefusal) => void) | null;
}

/** One `OnConnectionChanged` call, so the same function registered twice is heard twice. */
interface ConnectionListener {
  readonly onChanged: (connection: ServerConnection) => void;
}

/**
 * The browser's answer to IServerEvents — the C# contract's twin, resolved by interface name: the
 * page's one connection to the server's events, shared by every component that subscribes, each
 * payload revived as the C# type its topic carries.
 *
 * The connection opens with the first subscription and closes when none is left at the end of the
 * task, so a render that unmounts one subscriber and mounts the next keeps it. Each topic is bound
 * by a request once the connection has its id, and bound again on every connection that replaces
 * it; the requests for one topic go one at a time, so a release and a bind of it never cross on the
 * wire. `connected` is reported when the topics live at that moment are bound, so a component that
 * asks the server for what it missed asks once nothing more can be missed.
 *
 * A bind the server could not answer for (it did not know the connection, or the request did not
 * reach it) is asked again, a few times, before the page is told: a stream that has just ended is
 * still the page's own until the browser sees it end, and its next connection binds the topic.
 */
export class WebServerEvents {
  /** The topics the page holds, by name, each with its subscribers. */
  private readonly topics = new Map<string, Set<Subscriber>>();
  /** The topics the server holds on the current connection. */
  private readonly bound = new Set<string>();
  /** The topics whose requests are under way, each with the run that makes them. */
  private readonly syncing = new Map<string, Promise<void>>();
  /** How many binds in a row of each topic the server could not answer for. */
  private readonly misses = new Map<string, number>();
  private readonly listeners = new Set<ConnectionListener>();
  private readonly reports: ServerEventsListener = {
    connected: (connection) => this.connected(connection),
    message: (topic, payload, id) => this.deliver(topic, payload, id),
    dropped: () => this.dropped(),
  };
  private state: ServerConnectionStateValue = 'disconnected';
  private lastEventId: string | null = null;
  private connectionId: string | null = null;
  private closing: ReturnType<typeof setTimeout> | null = null;

  constructor(
    private readonly transport: ServerEventsTransport = new ServerEventStream(),
    private readonly serves: () => boolean = serverServesEvents,
  ) {}

  get connection(): ServerConnection {
    return new ServerConnection(this.state, this.lastEventId);
  }

  subscribe(
    topic: ServerTopic,
    onEvent: (payload: unknown) => void,
    onRefused?: ((refusal: ServerTopicRefusal) => void) | null,
  ): Subscription {
    const subscriber: Subscriber = { topic, onEvent, onRefused: onRefused ?? null };
    let subscribers = this.topics.get(topic.name);
    if (subscribers === undefined) this.topics.set(topic.name, (subscribers = new Set()));
    subscribers.add(subscriber);
    if (this.serves()) {
      this.open();
      void this.sync(topic.name);
    } else {
      // Refused as the server refuses a topic it knows nothing of, once the caller has its
      // subscription, and without a stream the server would never answer.
      void Promise.resolve().then(() => this.refuse(topic.name, 'unknown', NO_EVENTS));
    }
    let disposed = false;
    return {
      dispose: () => {
        if (disposed) return;
        disposed = true;
        this.unsubscribe(subscriber);
      },
    };
  }

  onConnectionChanged(onChanged: (connection: ServerConnection) => void): Subscription {
    const listener: ConnectionListener = { onChanged };
    this.listeners.add(listener);
    return {
      dispose: () => {
        this.listeners.delete(listener);
      },
    };
  }

  private open(): void {
    if (this.closing !== null) {
      clearTimeout(this.closing);
      this.closing = null;
      // Kept open after all: what its end would have released is released by request instead.
      for (const name of this.bound) if (!this.topics.has(name)) void this.sync(name);
    }
    if (this.state !== 'disconnected') return;
    this.change('connecting');
    this.transport.open(this.reports);
  }

  private unsubscribe(subscriber: Subscriber): void {
    const name = subscriber.topic.name;
    const subscribers = this.topics.get(name);
    // Gone already when the server refused it, or held by a later subscription to the same name.
    if (subscribers === undefined || !subscribers.delete(subscriber) || subscribers.size > 0)
      return;
    this.topics.delete(name);
    this.misses.delete(name);
    void this.sync(name);
    this.closeWhenIdle();
  }

  /** Closes the connection at the end of the task, unless a subscription arrived in the meantime. */
  private closeWhenIdle(): void {
    if (this.topics.size > 0 || this.closing !== null || this.state === 'disconnected') return;
    this.closing = setTimeout(() => {
      this.closing = null;
      if (this.topics.size > 0) return;
      // The server releases every topic of a connection that ends: no request is made for them.
      this.transport.close();
      this.connectionId = null;
      this.bound.clear();
      this.change('disconnected');
    }, 0);
  }

  private connected(connection: string): void {
    this.connectionId = connection;
    this.bound.clear();
    const binding = [...this.topics.keys()].map((name) => this.sync(name));
    void Promise.all(binding).then(() => {
      if (this.connectionId === connection) this.change('connected');
    });
  }

  private dropped(): void {
    this.connectionId = null;
    this.bound.clear();
    if (this.state !== 'disconnected') this.change('reconnecting');
  }

  private deliver(name: string, payload: unknown, id: string | null): void {
    if (id !== null) this.lastEventId = id;
    const subscribers = this.topics.get(name);
    if (subscribers === undefined) return;
    for (const subscriber of [...subscribers]) {
      // One heard before it may have disposed it.
      if (!subscribers.has(subscriber)) continue;
      const spec = subscriber.topic.spec;
      try {
        subscriber.onEvent(spec == null ? payload : hydrate(payload, spec));
      } catch (error) {
        console.error(`[eQuantic.UI] a subscriber to the topic '${name}' threw`, error);
      }
    }
  }

  /**
   * The run that brings the server's binding of one topic to what the page holds. Stored before its
   * first step, and removed by its last, in the same turn as its last look at the page, so a change
   * made after that look starts a new run rather than joining one that has finished.
   */
  private sync(name: string): Promise<void> {
    let running = this.syncing.get(name);
    if (running === undefined) {
      running = Promise.resolve().then(() => this.reconcile(name));
      this.syncing.set(name, running);
    }
    return running;
  }

  private async reconcile(name: string): Promise<void> {
    try {
      for (;;) {
        const connection = this.connectionId;
        const wanted = this.topics.has(name);
        if (connection === null || wanted === this.bound.has(name)) return;
        if (!wanted) {
          // A page that holds nothing closes its connection at the end of the task, and the server
          // releases what a connection held when it ends; a subscription that arrives first keeps it
          // open, and syncs this again (open).
          if (this.topics.size === 0) return;
          await this.transport.release(connection, name);
          if (connection === this.connectionId) this.bound.delete(name);
          continue;
        }
        const outcome = await this.transport.bind(connection, name);
        // Asked on a connection that has since been replaced: bind it on the one there is now.
        if (connection !== this.connectionId) continue;
        if (outcome === 'bound') {
          this.bound.add(name);
          this.misses.delete(name);
        } else if (outcome === 'gone' || outcome === 'failed') {
          // No answer about the topic itself. A stream that has just ended is unknown to the server
          // before the browser sees it end, and a proxy answers for an instance that is going away:
          // asked again, the page's next connection binds it. A page whose requests keep missing its
          // stream is told once it has asked enough.
          const misses = (this.misses.get(name) ?? 0) + 1;
          if (misses >= BIND_ATTEMPTS) {
            this.misses.delete(name);
            this.refuse(name, 'failed', outcome === 'gone' ? GONE : FAILED);
          } else {
            this.misses.set(name, misses);
            await delay(FIRST_BIND_RETRY_MS * 2 ** (misses - 1));
          }
        } else this.refuse(name, outcome);
      }
    } finally {
      this.syncing.delete(name);
    }
  }

  private refuse(name: string, reason: ServerTopicRefusalReasonValue, why?: string): void {
    const subscribers = this.topics.get(name);
    if (subscribers === undefined) return;
    this.topics.delete(name);
    this.misses.delete(name);
    const refusal = new ServerTopicRefusal(name, reason);
    let unheard = false;
    for (const subscriber of subscribers) {
      if (subscriber.onRefused === null) {
        unheard = true;
        continue;
      }
      try {
        subscriber.onRefused(refusal);
      } catch (error) {
        console.error(`[eQuantic.UI] a refusal handler of the topic '${name}' threw`, error);
      }
    }
    // A reason the app has to act on is an error; a refusal no subscriber listens for, a warning.
    if (why !== undefined)
      console.error(`[eQuantic.UI] the server did not bind the topic '${name}' (${reason})${why}`);
    else if (unheard) console.warn(`[eQuantic.UI] the server did not bind the topic '${name}' (${reason})`);
    this.closeWhenIdle();
  }

  private change(state: ServerConnectionStateValue): void {
    if (this.state === state) return;
    this.state = state;
    const connection = this.connection;
    for (const listener of [...this.listeners]) {
      if (!this.listeners.has(listener)) continue;
      try {
        listener.onChanged(connection);
      } catch (error) {
        console.error('[eQuantic.UI] a server connection listener threw', error);
      }
    }
  }
}

/** How many times in a row a bind the server could not answer for is asked before the page is told. */
const BIND_ATTEMPTS = 4;

/** How long the first ask again waits; each one after it waits twice as long. */
const FIRST_BIND_RETRY_MS = 500;

/**
 * Why a bind the server kept answering with "no such connection" fails while the page's connection
 * stands: the requests reach an instance other than the one that holds the stream.
 */
const GONE =
  ": the requests reached a server that does not hold the page's connection. Behind a load " +
  "balancer with several instances, a page's requests must reach the instance that holds its " +
  'stream (session affinity).';

/** Why a bind fails that the server never answered: it could not be reached, or it failed. */
const FAILED =
  ': the server could not be reached, or answered with an error, each time it was asked. Its log ' +
  "says why when it answered: a topic's authorization that throws, or a policy it does not know.";

/** Why every topic of a page is refused when the app's server serves no events. */
const NO_EVENTS =
  ': the server serves no events. Call UseServerEvents in AddUI, with a Topic for each template ' +
  'its pages may subscribe to.';

/** Whether the page's server serves events, as the shell says: absent, it is assumed to. */
function serverServesEvents(): boolean {
  return typeof window === 'undefined' || window.__EQ_CONFIG?.serverEvents !== false;
}

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}
