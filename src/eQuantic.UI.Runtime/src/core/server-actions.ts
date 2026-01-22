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
  async invoke<T = unknown>(actionId: string, args: unknown[] = []): Promise<T> {
    const response = await fetch(this.baseUrl, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        actionId,
        arguments: args,
      }),
    });

    const data: ServerActionResponse<T> = await response.json();

    if (!data.success) {
      throw new Error(data.error || `Server action failed: ${actionId}`);
    }

    return data.result as T;
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
