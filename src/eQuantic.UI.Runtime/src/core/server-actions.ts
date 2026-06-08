/**
 * eQuantic.UI Runtime - Server Actions Bridge
 *
 * Client-side bridge for invoking server-side actions.
 */

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
      const response = await fetch(this.baseUrl, {
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
