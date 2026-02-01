/**
 * Development Error Overlay - Similar to Next.js
 * Shows beautiful error UI during development
 */

const isDev = typeof window !== 'undefined' && window.__EQ_DEV__;

interface ErrorInfo {
  message: string;
  stack?: string;
  componentStack?: string;
}

class ErrorOverlay {
  private overlay: HTMLDivElement | null = null;
  private errors: ErrorInfo[] = [];

  show(error: ErrorInfo) {
    if (!isDev) return; // Only in development

    this.errors.push(error);
    this.render();
  }

  clear() {
    this.errors = [];
    if (this.overlay) {
      this.overlay.remove();
      this.overlay = null;
    }
  }

  private render() {
    if (!this.overlay) {
      this.overlay = document.createElement('div');
      this.overlay.id = 'equantic-error-overlay';
      document.body.appendChild(this.overlay);
    }

    const error = this.errors[this.errors.length - 1];

    this.overlay.innerHTML = `
      <style>
        #equantic-error-overlay {
          position: fixed;
          top: 0;
          left: 0;
          right: 0;
          bottom: 0;
          background: rgba(0, 0, 0, 0.9);
          color: #fff;
          z-index: 999999;
          display: flex;
          flex-direction: column;
          font-family: 'Menlo', 'Monaco', 'Courier New', monospace;
          font-size: 14px;
        }

        #equantic-error-overlay .header {
          background: #e53e3e;
          padding: 20px 30px;
          font-size: 18px;
          font-weight: bold;
          display: flex;
          justify-content: space-between;
          align-items: center;
        }

        #equantic-error-overlay .close {
          background: rgba(255, 255, 255, 0.2);
          border: none;
          color: white;
          padding: 8px 16px;
          border-radius: 4px;
          cursor: pointer;
          font-size: 14px;
        }

        #equantic-error-overlay .close:hover {
          background: rgba(255, 255, 255, 0.3);
        }

        #equantic-error-overlay .content {
          flex: 1;
          overflow: auto;
          padding: 30px;
        }

        #equantic-error-overlay .message {
          font-size: 16px;
          margin-bottom: 20px;
          line-height: 1.6;
        }

        #equantic-error-overlay .stack {
          background: rgba(255, 255, 255, 0.05);
          border-left: 3px solid #e53e3e;
          padding: 15px;
          overflow-x: auto;
          white-space: pre-wrap;
          word-break: break-word;
          font-size: 13px;
          line-height: 1.5;
        }

        #equantic-error-overlay .footer {
          background: rgba(255, 255, 255, 0.05);
          padding: 15px 30px;
          font-size: 12px;
          color: rgba(255, 255, 255, 0.6);
        }
      </style>

      <div class="header">
        <span>⚠️ Build Error</span>
        <button class="close" onclick="document.getElementById('equantic-error-overlay').remove()">
          Close (Esc)
        </button>
      </div>

      <div class="content">
        <div class="message">${this.escapeHtml(error.message)}</div>
        ${error.stack ? `<div class="stack">${this.escapeHtml(error.stack)}</div>` : ''}
      </div>

      <div class="footer">
        This error overlay only appears in development. Fix the error to continue.
      </div>
    `;

    // Close on Escape
    const closeHandler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        this.clear();
        document.removeEventListener('keydown', closeHandler);
      }
    };
    document.addEventListener('keydown', closeHandler);
  }

  private escapeHtml(text: string): string {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
  }
}

export const errorOverlay = new ErrorOverlay();

// Capture unhandled errors
if (isDev && typeof window !== 'undefined') {
  window.addEventListener('error', (event) => {
    errorOverlay.show({
      message: event.message,
      stack: event.error?.stack,
    });
  });

  window.addEventListener('unhandledrejection', (event) => {
    errorOverlay.show({
      message: `Unhandled Promise Rejection: ${event.reason}`,
      stack: event.reason?.stack,
    });
  });
}
