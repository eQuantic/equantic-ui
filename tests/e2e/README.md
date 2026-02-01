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

## Estrutura

```
tests/e2e/
├── playwright.config.ts    # Configuração do Playwright
├── tests/
│   └── ssr-hydration.spec.ts  # Testes de SSR/hidratação
└── package.json
```
