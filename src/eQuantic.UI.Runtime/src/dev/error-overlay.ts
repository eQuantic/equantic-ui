/**
 * Development Error Overlay - Similar to Next.js
 * Shows beautiful error UI during development
 */

import { remapStackTrace, RemappedFrame } from './stack-remapper';
import { RawSourceMap } from './source-map';

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
    if (!isDev) return;
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
          background: rgba(10, 10, 12, 0.8);
          backdrop-filter: blur(20px);
          -webkit-backdrop-filter: blur(20px);
          color: #f8fafc;
          z-index: 999999;
          display: flex;
          align-items: center;
          justify-content: center;
          font-family: 'Inter', -apple-system, system-ui, sans-serif;
          padding: 40px;
        }

        #equantic-error-overlay .card {
          background: rgba(30, 30, 35, 0.7);
          border: 1px solid rgba(255, 255, 255, 0.1);
          border-radius: 20px;
          width: 100%;
          max-width: 900px;
          max-height: 90vh;
          display: flex;
          flex-direction: column;
          box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.5);
          overflow: hidden;
          animation: eq-fade-in 0.3s ease-out;
        }

        @keyframes eq-fade-in {
          from { opacity: 0; transform: translateY(20px); }
          to { opacity: 1; transform: translateY(0); }
        }

        #equantic-error-overlay .header {
          padding: 24px 32px;
          border-bottom: 1px solid rgba(255, 255, 255, 0.05);
          display: flex;
          justify-content: space-between;
          align-items: center;
          background: rgba(229, 62, 62, 0.1);
        }

        #equantic-error-overlay .header-title {
          display: flex;
          align-items: center;
          gap: 12px;
          font-weight: 700;
          font-size: 18px;
          color: #feb2b2;
        }

        #equantic-error-overlay .header-title i {
            font-style: normal;
            background: #e53e3e;
            color: white;
            width: 24px;
            height: 24px;
            display: flex;
            align-items: center;
            justify-content: center;
            border-radius: 6px;
            font-size: 14px;
        }

        #equantic-error-overlay .close-btn {
          background: rgba(255, 255, 255, 0.05);
          border: 1px solid rgba(255, 255, 255, 0.1);
          color: #cbd5e1;
          padding: 8px 16px;
          border-radius: 8px;
          cursor: pointer;
          font-size: 13px;
          font-weight: 500;
          transition: all 0.2s;
        }

        #equantic-error-overlay .close-btn:hover {
          background: rgba(255, 255, 255, 0.1);
          color: white;
        }

        #equantic-error-overlay .content {
          flex: 1;
          overflow: auto;
          padding: 32px;
        }

        #equantic-error-overlay .error-message {
          font-size: 20px;
          font-weight: 600;
          margin-bottom: 24px;
          line-height: 1.4;
          color: #fff;
          word-break: break-word;
        }

        #equantic-error-overlay .section-label {
          text-transform: uppercase;
          font-size: 11px;
          font-weight: 700;
          letter-spacing: 0.1em;
          color: #94a3b8;
          margin-bottom: 12px;
          display: flex;
          align-items: center;
          gap: 8px;
        }

        #equantic-error-overlay .section-label::after {
            content: '';
            flex: 1;
            height: 1px;
            background: rgba(255, 255, 255, 0.05);
        }

        #equantic-error-overlay .code-block {
          background: #0f172a;
          border-radius: 12px;
          padding: 20px;
          margin-bottom: 24px;
          font-family: 'JetBrains Mono', 'Fira Code', 'Menlo', monospace;
          font-size: 13px;
          line-height: 1.6;
          overflow-x: auto;
          border: 1px solid rgba(255, 255, 255, 0.05);
        }

        #equantic-error-overlay .stack-trace {
          color: #94a3b8;
          font-family: 'JetBrains Mono', 'Fira Code', 'Menlo', monospace;
          font-size: 12px;
          line-height: 1.6;
          white-space: pre-wrap;
          word-break: break-all;
        }

        #equantic-error-overlay .stack-line {
            display: block;
            padding: 4px 8px;
            border-radius: 4px;
            margin-bottom: 2px;
        }

        #equantic-error-overlay .stack-line:hover {
            background: rgba(255, 255, 255, 0.05);
            color: #cbd5e1;
        }

        #equantic-error-overlay .stack-file { color: #60a5fa; font-weight: 500; }
        #equantic-error-overlay .stack-pos { color: #f472b6; }

        /* Syntax Highlighting */
        .eq-hl-keyword { color: #c678dd; }
        .eq-hl-string { color: #98c379; }
        .eq-hl-number { color: #d19a66; }
        .eq-hl-comment { color: #5c6370; font-style: italic; }
        .eq-hl-func { color: #61afef; }
        .eq-hl-attr { color: #e06c75; }

        #equantic-error-overlay .footer {
          padding: 16px 32px;
          background: rgba(0, 0, 0, 0.2);
          border-top: 1px solid rgba(255, 255, 255, 0.05);
          font-size: 12px;
          color: #64748b;
          display: flex;
          justify-content: center;
        }
      </style>

      <div class="card">
        <div class="header">
          <div class="header-title">
            <i>!</i>
            <span>Build Error</span>
          </div>
          <button class="close-btn" onclick="document.getElementById('equantic-error-overlay').remove()">
            Dismiss (Esc)
          </button>
        </div>

        <div class="content">
          <div class="error-message">${this.escapeHtml(error.message)}</div>

          <div class="section-label" id="eq-source-label">Source Context</div>
          <div class="code-block" id="eq-source-context">${this.highlightCode(error.stack || '')}</div>

          ${
            error.stack
              ? `
            <div class="section-label" id="eq-stack-label">Call Stack</div>
            <div class="stack-trace" id="eq-stack-trace">${this.formatStack(error.stack)}</div>
          `
              : ''
          }
        </div>

        <div class="footer">
          Dev Mode Overlay &bull; Fix the error above to resume.
        </div>
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

    // Asynchronously remap the JS stack back to C# via source maps and upgrade the UI in place.
    if (error.stack) void this.enhanceWithSourceMaps(error.stack);
  }

  /**
   * Translates the JS stack trace to the original C# frames (using the bundle's source maps) and
   * rewrites the Call Stack + Source Context in place. Degrades silently to the JS view on failure.
   */
  private async enhanceWithSourceMaps(stack: string): Promise<void> {
    let frames: RemappedFrame[];
    try {
      frames = await remapStackTrace(stack, (url) => this.fetchSourceMap(url));
    } catch {
      return;
    }

    if (!this.overlay || !frames.some((f) => f.mapped)) return; // nothing mapped → keep JS view

    const stackEl = this.overlay.querySelector('#eq-stack-trace');
    const stackLabel = this.overlay.querySelector('#eq-stack-label');
    if (stackEl) stackEl.innerHTML = this.formatRemappedStack(frames);
    if (stackLabel) stackLabel.textContent = 'Call Stack (C#)';

    // Show a C# source snippet around the first mapped frame that carries source content.
    const top = frames.find((f) => f.mapped && f.sourceContent);
    if (top) {
      const ctxEl = this.overlay.querySelector('#eq-source-context');
      const ctxLabel = this.overlay.querySelector('#eq-source-label');
      if (ctxEl) ctxEl.innerHTML = this.renderCSharpSnippet(top);
      if (ctxLabel)
        ctxLabel.textContent = `Source Context — ${this.escapeHtml(top.file)}:${top.line}`;
    }
  }

  private async fetchSourceMap(jsUrl: string): Promise<RawSourceMap | null> {
    try {
      // Bun emits "<file>.js.map" next to the bundle; the sourceMappingURL points at it.
      const response = await fetch(`${jsUrl}.map`);
      if (!response.ok) return null;
      return (await response.json()) as RawSourceMap;
    } catch {
      return null;
    }
  }

  private formatRemappedStack(frames: RemappedFrame[]): string {
    return frames
      .map((f) => {
        const func = f.func ? this.escapeHtml(f.func) : '<anonymous>';
        const tag = f.mapped ? '' : ' <span class="stack-pos">(js)</span>';
        return (
          `<span class="stack-line">at <span class="eq-hl-func">${func}</span> ` +
          `(<span class="stack-file">${this.escapeHtml(f.file)}</span>:` +
          `<span class="stack-pos">${f.line}</span>:<span class="stack-pos">${f.column}</span>)${tag}</span>`
        );
      })
      .join('');
  }

  private renderCSharpSnippet(frame: RemappedFrame): string {
    const content = frame.sourceContent ?? '';
    const lines = content.split('\n');
    const target = frame.line; // 1-based
    const start = Math.max(1, target - 2);
    const end = Math.min(lines.length, target + 2);

    const out: string[] = [];
    for (let n = start; n <= end; n++) {
      const isTarget = n === target;
      const gutter = String(n).padStart(4, ' ');
      const text = this.escapeHtml(lines[n - 1] ?? '');
      out.push(
        `<div class="stack-line"${isTarget ? ' style="background:rgba(229,62,62,0.15)"' : ''}>` +
          `<span class="stack-pos">${gutter}</span>  ${isTarget ? '<span class="eq-hl-keyword">▶</span> ' : '  '}${text}` +
          `</div>`,
      );
    }
    return out.join('');
  }

  private highlightCode(stack: string): string {
    const lines = stack.split('\n');
    const firstLine = lines[0] || '';

    // If it looks like a stack trace line, don't try to highlight as code
    if (firstLine.trim().startsWith('at ')) {
      return this.escapeHtml(firstLine);
    }

    // Basic regex highlighter
    let highlighted = this.escapeHtml(firstLine);

    // Keywords
    const keywords = [
      'function',
      'class',
      'const',
      'let',
      'var',
      'if',
      'else',
      'for',
      'while',
      'return',
      'async',
      'await',
      'import',
      'export',
      'public',
      'private',
      'protected',
      'static',
      'readonly',
      'new',
      'this',
      'throw',
      'try',
      'catch',
      'finally',
    ];
    const kwRegex = new RegExp(`\\b(${keywords.join('|')})\\b`, 'g');
    highlighted = highlighted.replace(kwRegex, '<span class="eq-hl-keyword">$1</span>');

    // Strings
    highlighted = highlighted.replace(/(["'`].*?["'`])/g, '<span class="eq-hl-string">$1</span>');

    // Numbers
    highlighted = highlighted.replace(/\b(\d+)\b/g, '<span class="eq-hl-number">$1</span>');

    // Comments
    highlighted = highlighted.replace(/(\/\/.*$)/g, '<span class="eq-hl-comment">$1</span>');

    return highlighted;
  }

  private formatStack(stack: string): string {
    return stack
      .split('\n')
      .filter((line) => line.trim().startsWith('at'))
      .map((line) => {
        const match =
          line.match(/(at\s+)?(.*?)\s+\((.*?):(\d+):(\d+)\)/) ||
          line.match(/(at\s+)?(.*?):(.*?):(\d+):(\d+)/);
        if (match) {
          const [, , func, file, lineNum, col] = match;
          return `<span class="stack-line">at <span class="eq-hl-func">${this.escapeHtml(func)}</span> (<span class="stack-file">${this.escapeHtml(file)}</span>:<span class="stack-pos">${lineNum}</span>:<span class="stack-pos">${col}</span>)</span>`;
        }
        return `<span class="stack-line">${this.escapeHtml(line)}</span>`;
      })
      .join('');
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
