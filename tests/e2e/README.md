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
the sample's `/passkeys` page runs a real Web Authentication ceremony through `IPasskeys` (#1017):

1. **A passkey is created, signs in, and is not created twice**: the sample's relying party finds the
   challenge it issued in each answer's client data, and a second creation, which excludes the passkey
   just registered, answers `AlreadyRegistered`.
2. **A page outside a secure context has no passkey to offer**: `passkeys.test` is mapped to this
   machine, so the page is served over plain http from a name that is not localhost, and `CreateAsync`
   answers `Unavailable`.

## Estrutura

```
tests/e2e/
├── playwright.config.ts    # Configuração do Playwright
├── tests/
│   ├── passkeys.spec.ts       # Passkeys through IPasskeys (#1017)
│   └── ssr-hydration.spec.ts  # Testes de SSR/hidratação
└── package.json
```
