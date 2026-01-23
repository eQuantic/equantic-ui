// eQuantic.UI Client Runtime
// Handles dynamic imports and component mounting

// Re-export Runtime Components
export * from '../../eQuantic.UI.Runtime/src/index';

declare global {
    interface Window {
        __EQ_CONFIG?: {
            page?: string;
            version?: string;
        };
    }
}

/**
 * Bootstraps the eQuantic application
 */
export async function boot() {
    console.log('eQuantic.UI Runtime (v2.1) initializing...');

    try {
        const config = window.__EQ_CONFIG || {};
        const pageName = resolvePageName(config);
        
        if (!pageName) {
            handleNoPage();
            return;
        }

        await loadAndMountPage(pageName, config.version);
    } catch (err) {
        handleGlobalError(err as Error);
    }
}

/**
 * Resolves the page name from Server Injection or Query String
 */
function resolvePageName(config: any): string | null {
    // 1. Server Injection (Preferred for Clean URLs)
    if (config.page) {
        return config.page;
    }

    // 2. Query String (Legacy/Debug)
    const params = new URLSearchParams(window.location.search);
    return params.get('page');
}

/**
 * Loads the page module and mounts the component
 */
async function loadAndMountPage(pageName: string, version?: string) {
    const root = document.getElementById('app');
    if (!root) throw new Error("Root element #app not found");

    root.innerHTML = '<div class="loading">Loading resource...</div>';

    try {
        const cacheBuster = version ? `?v=${version}` : '';
        const modulePath = `/_equantic/${pageName}.js${cacheBuster}`;
        console.log(`eQuantic.UI: Loading module '${modulePath}'...`);
        
        // Dynamic import with cache busting
        const module = await import(modulePath);
        
        const ComponentClass = module[pageName];
        if (!ComponentClass) {
            throw new Error(`Module ${pageName} does not export class '${pageName}'`);
        }

        console.log(`eQuantic.UI: Mounting ${pageName}...`);
        const app = new ComponentClass();

        // Check for mount/render methods
        if (typeof app.mount === 'function') {
            root.innerHTML = '';
            app.mount(root);
        } else if (typeof app.render === 'function') {
            root.innerHTML = '';
            app.render(root);
        } else {
            console.warn("Component missing mount/render method", app);
            root.innerHTML = `
                <div style="padding: 2rem; color: #166534; background: #dcfce7; border-radius: 8px;">
                    <h1 style="margin:0">✅ Loaded ${pageName}</h1>
                    <p>Instance created but no <code>mount()</code> method found.</p>
                </div>
            `;
        }

    } catch (e: any) {
        // Handle 404s specifically
        if (e.message?.includes('Failed to fetch') || e.message?.includes('Cannot find module')) {
            render404(root, pageName);
        } else {
            throw e;
        }
    }
}

function handleNoPage() {
    if (window.location.pathname === '/' || window.location.pathname === '') {
        renderWelcome(document.getElementById('app')!);
    } else {
        render404(document.getElementById('app')!, window.location.pathname);
    }
}

function handleGlobalError(error: Error) {
    console.error("Runtime Error:", error);
    const root = document.getElementById('app');
    if (root) {
        root.innerHTML = `
            <div style="color: #991b1b; padding: 20px; text-align: center; font-family: system-ui;">
                <h3>Application Error</h3>
                <pre style="background: #fef2f2; padding: 10px; border-radius: 4px; display: inline-block; text-align: left;">${error.message}</pre>
            </div>
        `;
    }
}

// UI Renderers (Keep simple HTML/CSS for now)

function render404(root: HTMLElement, resource: string) {
    root.innerHTML = `
        <div style='display: flex; flex-direction: column; align-items: center; justify-content: center; height: 100vh; font-family: system-ui, sans-serif; text-align: center;'>
            <h1 style='font-size: 6rem; margin: 0; color: #cbd5e1;'>404</h1>
            <h2 style='font-size: 2rem; margin: 1rem 0; color: #1e293b;'>Page Not Found</h2>
            <p style='color: #64748b; font-size: 1.2rem;'>The resource '<strong>${escapeHtml(resource)}</strong>' does not exist.</p>
            <a href='/' style='margin-top: 2rem; padding: 0.75rem 1.5rem; background: #2563eb; color: white; text-decoration: none; border-radius: 0.5rem; font-weight: 500;'>Go Home</a>
        </div>
    `;
}

function renderWelcome(root: HTMLElement) {
    root.innerHTML = `
        <div style='text-align:center; padding: 2rem; font-family: sans-serif; height: 100vh; display: flex; flex-direction: column; justify-content: center; align-items: center;'>
            <h1 style="color: #2563eb; font-size: 2.5rem;">Welcome to eQuantic.UI</h1>
            <p style="color: #64748b;">No default page configured.</p>
        </div>
    `;
}

function escapeHtml(unsafe: string) {
    return unsafe
         .replace(/&/g, "&amp;")
         .replace(/</g, "&lt;")
         .replace(/>/g, "&gt;")
         .replace(/"/g, "&quot;")
         .replace(/'/g, "&#039;");
}

// Auto-start
boot();
