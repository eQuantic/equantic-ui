# eQuantic.UI Build Flow

Este documento descreve o fluxo de build do eQuantic.UI, demonstrando como o framework mantém **zero dependências externas** para o consumidor.

## Fluxo Visual

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           DESENVOLVIMENTO (source tree)                      │
└─────────────────────────────────────────────────────────────────────────────┘

  reconciler.ts, component.ts, etc.
         │
         ▼
  ┌──────────────────────────────────┐
  │  npm run build                   │  (apenas durante desenvolvimento)
  │  (eQuantic.UI.Runtime)           │
  └──────────────────────────────────┘
         │
         ▼
  dist/index.js (runtime compilado)
         │
         │
  boot.ts ──────imports────────┘
         │
         ▼
  ┌──────────────────────────────────┐
  │  dotnet build                    │
  │  (eQuantic.UI.Server)            │
  │                                  │
  │  ResolveBunForServer target:     │
  │  ├─ Procura Bun em:              │
  │  │  Runtime.Osx64/tools/bun/     │
  │  │  Runtime.Win64/tools/bun/     │
  │  │  Runtime.Linux64/tools/bun/   │
  │  ├─ Extrai do .zip se necessário │
  │  └─ chmod +x (Unix)              │
  │                                  │
  │  BundleRuntime target:           │
  │  └─ "$(_BunPath)" build boot.ts  │
  └──────────────────────────────────┘
         │
         ▼
  wwwroot/runtime.js (embebido no Server.dll)
         │
         ▼
  ┌──────────────────────────────────┐
  │  dotnet pack                     │
  │  (eQuantic.UI.Server)            │
  └──────────────────────────────────┘
         │
         ▼
  artifacts/packages/eQuantic.UI.Server.0.1.1.nupkg


┌─────────────────────────────────────────────────────────────────────────────┐
│                        CONSUMIDOR (projeto do cliente)                       │
└─────────────────────────────────────────────────────────────────────────────┘

  MyApp.csproj
  ├─ Sdk="eQuantic.UI.Sdk/0.1.1"
  └─ PackageReference: eQuantic.UI.Server, eQuantic.UI.Runtime

         │
         ▼
  ┌──────────────────────────────────┐
  │  dotnet restore                  │
  │                                  │
  │  NuGet instala pacotes:          │
  │  ├─ eQuantic.UI.Sdk              │
  │  ├─ eQuantic.UI.Server           │
  │  ├─ eQuantic.UI.Runtime          │
  │  │  └─ (meta-package)            │
  │  └─ eQuantic.UI.Runtime.Osx64    │  ◄── Bun embarcado aqui!
  │     └─ tools/bun/bun-darwin.zip  │
  └──────────────────────────────────┘
         │
         ▼
  ┌──────────────────────────────────┐
  │  dotnet build                    │
  │                                  │
  │  SDK.targets executa:            │
  │                                  │
  │  1. ResolveBunZipPath            │
  │     └─ $(PkgeQuantic_UI_Runtime_ │
  │        Osx64)/tools/bun/*.zip    │
  │                                  │
  │  2. EnsureBunExtracted           │
  │     ├─ Unzip se necessário       │
  │     └─ chmod +x (Unix)           │
  │                                  │
  │  3. ResolveBunPath               │
  │     └─ Define $(BunPath)         │
  │                                  │
  │  4. CompileEQuanticUI            │
  │     └─ dotnet eqc.dll ... --bun  │
  │        "$(BunPath)"              │
  │                                  │
  │  5. BuildCSS (Tailwind)          │
  │     └─ "$(BunPath)" x            │
  │        @tailwindcss/cli ...      │
  └──────────────────────────────────┘
         │
         ▼
  wwwroot/_equantic/*.js (componentes compilados)
         │
         ▼
  ┌──────────────────────────────────┐
  │  dotnet run                      │
  │                                  │
  │  Server serve:                   │
  │  ├─ runtime.js (do Server.dll)   │
  │  └─ *.js (de wwwroot/_equantic)  │
  └──────────────────────────────────┘
         │
         ▼
  Browser carrega aplicação
```

## Fonte do Bun por Componente

| Componente | Fonte do Bun |
|------------|--------------|
| **Server** (build do pacote) | `eQuantic.UI.Runtime.{OS}/tools/bun/` (source tree) |
| **SDK** (consumidor) | `$(PkgeQuantic_UI_Runtime_{OS})/tools/bun/` (NuGet cache) |
| **Tailwind** | Usa `$(BunPath)` resolvido pelo SDK |

## Requisitos do Consumidor

O consumidor só precisa de:
- .NET SDK 8.0
- `dotnet restore` + `dotnet build`

**Nenhum Node.js, npm, ou Bun global necessário.**

## Arquivos Chave

| Arquivo | Responsabilidade |
|---------|------------------|
| `Sdk/Sdk.targets` | Resolve Bun do NuGet cache, compila componentes |
| `Server.csproj` | Resolve Bun do source tree, bundla runtime.js |
| `Tailwind.targets` | Usa `$(BunPath)` para gerar CSS |
| `Runtime.{OS}.csproj` | Empacota executável Bun para cada plataforma |

## MSBuild Targets (Ordem de Execução)

### No SDK (consumidor)

1. **ResolveBunZipPath** - Encontra o .zip do Bun no cache NuGet
2. **EnsureBunExtracted** - Extrai o executável se necessário
3. **ResolveBunPath** - Define `$(BunPath)` para uso posterior
4. **CompileEQuanticUI** - Transpila C# → TypeScript → JavaScript
5. **BuildCSS** (Tailwind) - Gera CSS com Tailwind CLI

### No Server (desenvolvimento)

1. **ResolveBunForServer** - Encontra Bun no source tree
2. **BundleRuntime** - Compila boot.ts → runtime.js
