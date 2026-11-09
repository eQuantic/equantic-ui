/**
 * eQuantic.UI Runtime - Server Actions Bridge
 *
 * Client-side bridge for invoking server-side actions.
 */

import { activeCulture } from '../utils/culture';

export interface ServerActionResponse<T = unknown> {
  success: boolean;
  result?: T;
  error?: string;
}

/**
 * Client for invoking server actions.
 */
export class ServerActionsClient {
  private readonly baseUrl: string;

  constructor(baseUrl: string = '/api/_equantic/actions') {
    this.baseUrl = baseUrl;
  }

  /**
   * The endpoint, naming the culture the page is in. The endpoint's address has no culture segment,
   * so the route that rendered `/pt-BR/...` answers nothing here, and the cookie or the browser's
   * `Accept-Language` chose the language a handler answered in, whatever the page was written in
   * (#580). The culture travels in the two query keys ASP.NET's own
   * `QueryStringRequestCultureProvider` reads, ahead of the cookie: the app's localization middleware
   * applies it, a culture the app does not support is refused there as any other, and a page with no
   * culture installed sends nothing and keeps that order.
   */
  private addressInCulture(): string {
    const { ui, format } = activeCulture();
    const query = new URLSearchParams();
    if (format.length > 0) query.set('culture', format);
    if (ui.length > 0) query.set('ui-culture', ui);
    const culture = query.toString();
    if (culture.length === 0) return this.baseUrl;
    return `${this.baseUrl}${this.baseUrl.includes('?') ? '&' : '?'}${culture}`;
  }

  /**
   * Invoke a server action.
   * @param actionId The action identifier (e.g., "Counter/Increment")
   * @param args Arguments to pass to the action
   */
  async invoke<T = unknown>(
    actionId: string,
    args: unknown[] = [],
    options: { timeoutMs?: number } = {},
  ): Promise<T> {
    const { timeoutMs = 30000 } = options;
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), timeoutMs);

    try {
      const response = await fetch(this.addressInCulture(), {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          actionId,
          arguments: args,
        }),
        signal: controller.signal,
      });

      // Surface HTTP-level failures with their status/body instead of feeding a
      // (possibly HTML) error page straight into JSON.parse.
      if (!response.ok) {
        const body = await response.text().catch(() => '');
        throw new Error(
          `Server action ${actionId} failed with HTTP ${response.status}` +
            (body ? `: ${body}` : ''),
        );
      }

      let data: ServerActionResponse<T>;
      try {
        data = await response.json();
      } catch {
        throw new Error(`Server action ${actionId} returned a non-JSON response`);
      }

      if (!data.success) {
        throw new Error(data.error || `Server action failed: ${actionId}`);
      }

      return data.result as T;
    } catch (err) {
      if (controller.signal.aborted) {
        throw new Error(`Server action timed out after ${timeoutMs}ms: ${actionId}`);
      }
      throw err;
    } finally {
      clearTimeout(timeout);
    }
  }
}

/**
 * Global server actions client instance.
 */
let globalServerActionsClient: ServerActionsClient | null = null;

/**
 * Get the global server actions client.
 */
export function getServerActionsClient(): ServerActionsClient {
  if (!globalServerActionsClient) {
    globalServerActionsClient = new ServerActionsClient();
  }
  return globalServerActionsClient;
}

/**
 * Configure the server actions client.
 */
export function configureServerActions(baseUrl: string): void {
  globalServerActionsClient = new ServerActionsClient(baseUrl);
}

/**
 * Reset the server actions client (for testing).
 */
export function resetServerActionsClient(): void {
  globalServerActionsClient = null;
}
