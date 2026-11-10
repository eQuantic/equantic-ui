# eQuantic.UI E2E Tests

Testes end-to-end usando Playwright para validar SSR, hidratação e comportamento do framework.

## Instalação

```bash
npm install
npx playwright install
```

## Executar Testes

```bash
# Executar todos os testes
npm test

# Executar com UI
npm run test:headed

# Modo debug
npm run test:debug

# Interface interativa
npm run test:ui

# Ver relatório
npm run report
```

## Testes Implementados

### `ssr-hydration.spec.ts`

1. **SSR e CSR devem gerar HTML idêntico**: Compara o HTML gerado pelo servidor com o HTML após hidratação no cliente
2. **Todos os botões devem ter classes de tema aplicadas**: Valida que o tema está sendo aplicado corretamente
3. **Classes CSS devem ser idênticas entre SSR e CSR**: Compara classes de cada elemento
4. **Estado inicial deve ser hidratado corretamente**: Valida que os dados do estado foram hidratados
5. **Eventos devem funcionar após hidratação**: Valida que event handlers estão funcionando

### `passkeys.spec.ts`

Chrome's virtual authenticator, driven over the DevTools protocol, creates and signs with no hardware, so
the sample's `/passkeys` page runs a real Web Authentication ceremony through `IPasskeys` (#1017). Every
test also fails on an exception or a rejected promise the page left unhandled, which the runtime's
development overlay would have shown:

1. **A passkey is created, signs in, and is not created twice**: the sample's relying party finds the
   challenge it issued in each answer's client data, and a second creation, which excludes the passkey
   just registered, answers `AlreadyRegistered`.
2. **A server that does not take the answer leaves the page ready to try again**: the first answer
   handed to the server meets a 503, the page says so, and the next press registers the passkey.
3. **Options the server cannot renew are asked for again outside the press**: with the page's clock
   moved a minute on (`page.clock`), a renewal that fails leaves the page with no options and the
   buttons off, and the next renewal brings them back with nobody pressing anything.
4. **Options older than the page uses start no ceremony**: with the page's clock moved past them and no
   renewal fired, a press asks for new options instead, and the authenticator is never asked.
5. **A page outside a secure context has no passkey to offer**: `passkeys.test` is mapped to this
   machine, so the page is served over plain http from a name that is not localhost, and `CreateAsync`
   answers `Unavailable`.

### `uploads.spec.ts`

The sample's `/uploads` page sends files through `FileUpload` to the real upload endpoint (#1215):

1. **A file of three parts reaches the Server Action whole**: 20 MB go as one creation and three parts at
   their offsets, and the Server Action answers the exact length and SHA-256 of what it read.
2. **A file the policy does not accept is refused in its row**: `setup.exe` shows the policy's reason and
   nothing reaches the endpoint.
3. **The server's own refusal reaches the row in its words**: a creation answered 413 shows its body.

### `contrast.spec.ts`

Contrast levels (#340): the browser reads the system's setting as `prefers-contrast: more`, emulated
here with `page.emulateMedia`, and the app states nothing:

1. **Muted text steps up when the system asks for more contrast**, in light and in dark: every element
   painted in Muted paints the handoff's More leg, and steps back down with the setting.
2. **A screen the runtime builds after a navigation reads the same level**: no muted text on it paints
   its Standard leg.

`E2E_PORT` runs the server on another port, beside another checkout's.

## Estrutura

```
tests/e2e/
├── playwright.config.ts    # Configuração do Playwright
├── tests/
│   ├── contrast.spec.ts       # Contrast levels in a real browser (#340)
│   ├── passkeys.spec.ts       # Passkeys through IPasskeys (#1017)
│   ├── uploads.spec.ts        # Uploads through FileUpload (#1215)
│   └── ssr-hydration.spec.ts  # Testes de SSR/hidratação
└── package.json
```
