import { CancellationTokenSource } from "../runtime-exports";

export class ProviderRequest {
    static cancel(source: CancellationTokenSource | null, failed: ((exception: Error) => void) | null) {
        if (source == null) return;
        try {
            source.cancel();
        } catch (error: any) {
            failed?.(error);
        }
    }
}

