
// SignalR Client for eQuantic.UI
// This would typically use @microsoft/signalr, but to keep dependencies low for now we might use a CDN or bundled version.
// For this implementation, we assume signalr.js is loaded globally or we import it.

export class SignalRClient {
    private connection: any;

    constructor(hubUrl: string = "/_equantic/hub") {
        if (typeof window === 'undefined') return;

        // @ts-ignore
        if (window.signalR) {
            // @ts-ignore
            this.connection = new window.signalR.HubConnectionBuilder()
                .withUrl(hubUrl)
                .withAutomaticReconnect()
                .build();
            
            this.start();
        } else {
            console.warn("eQuantic.UI: SignalR library not found. Real-time features disabled.");
        }
    }

    private async start() {
        try {
            await this.connection.start();
            console.log("eQuantic.UI: Connected to SignalR Hub");
        } catch (err) {
            console.error("eQuantic.UI: SignalR Connection Error: ", err);
            setTimeout(() => this.start(), 5000);
        }
    }

    public on(eventName: string, callback: (...args: any[]) => void) {
        if (this.connection) {
            this.connection.on(eventName, callback);
        }
    }

    public off(eventName: string, callback: (...args: any[]) => void) {
        if (this.connection) {
            this.connection.off(eventName, callback);
        }
    }
}

// Global instance
export const signalR = new SignalRClient();
