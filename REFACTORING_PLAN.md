# Plano de Refatoração: CSharpToJsConverter

## 🎯 Objetivo

Refatorar `CSharpToJsConverter.cs` aplicando **Strategy Pattern** e princípios **SOLID** para tornar a conversão C# → TypeScript:
- ✅ **Extensível** - fácil adicionar novas conversões
- ✅ **Manutenível** - cada estratégia é independente
- ✅ **Testável** - estratégias podem ser testadas isoladamente
- ✅ **Escalável** - código não cresce descontroladamente

---

## 📊 Problema Atual

### Arquivo Monolítico: `CSharpToJsConverter.cs` (~700 linhas)

```csharp
public class CSharpToJsConverter
{
    // Converte TUDO em um único arquivo gigante
    public string ConvertExpression(ExpressionSyntax expression)
    {
        return expression switch
        {
            BinaryExpressionSyntax binary => ConvertBinary(binary),
            InvocationExpressionSyntax invocation => ConvertInvocation(invocation),
            MemberAccessExpressionSyntax member => ConvertMemberAccess(member),
            ObjectCreationExpressionSyntax obj => ConvertObjectCreation(obj),
            // ... 20+ tipos diferentes
            _ => expression.ToString()
        };
    }

    // Centenas de linhas de lógica condicional
    private string ConvertInvocation(...) { /* 100+ linhas */ }
    private string ConvertMemberAccess(...) { /* 80+ linhas */ }
    private string ConvertObjectCreation(...) { /* 50+ linhas */ }
    // ...
}
```

### Problemas:

1. ❌ **Violação SRP** (Single Responsibility Principle)
   - Uma classe faz TUDO: binary ops, invocations, member access, etc.

2. ❌ **Difícil manutenção**
   - Adicionar nova conversão = modificar classe gigante
   - Risco de quebrar outras conversões

3. ❌ **Difícil testar**
   - Testes precisam instanciar classe completa
   - Não dá para testar estratégias isoladamente

4. ❌ **Crescimento descontrolado**
   - Cada novo caso C# adiciona mais linhas
   - Arquivo tende a crescer para 1000+ linhas

---

## 🏗️ Arquitetura Proposta: Strategy Pattern

### Estrutura de Pastas

```
src/eQuantic.UI.Compiler/
├── CodeGen/
│   ├── CSharpToJsConverter.cs          (Orquestrador - 100 linhas)
│   ├── ConversionContext.cs            (Estado compartilhado)
│   │
│   ├── Strategies/                     ⭐ NOVO
│   │   ├── IConversionStrategy.cs
│   │   │
│   │   ├── Expressions/
│   │   │   ├── BinaryExpressionStrategy.cs
│   │   │   ├── InvocationExpressionStrategy.cs
│   │   │   ├── MemberAccessStrategy.cs
│   │   │   ├── ObjectCreationStrategy.cs
│   │   │   ├── LiteralStrategy.cs
│   │   │   ├── ConditionalExpressionStrategy.cs
│   │   │   └── ...
│   │   │
│   │   ├── Statements/
│   │   │   ├── IfStatementStrategy.cs
│   │   │   ├── ForEachStatementStrategy.cs
│   │   │   ├── ReturnStatementStrategy.cs
│   │   │   └── ...
│   │   │
│   │   ├── Linq/                       ⭐ Conversões LINQ
│   │   │   ├── SelectStrategy.cs       (Select → map)
│   │   │   ├── WhereStrategy.cs        (Where → filter)
│   │   │   ├── AnyStrategy.cs          (Any → some / length > 0)
│   │   │   ├── FirstStrategy.cs        (First → find)
│   │   │   └── ...
│   │   │
│   │   ├── Types/                      ⭐ Conversões de tipos
│   │   │   ├── NullableStrategy.cs     (.Value, .HasValue)
│   │   │   ├── EnumStrategy.cs         (Enum.Member → 'member')
│   │   │   ├── CollectionStrategy.cs   (List → Array)
│   │   │   └── ...
│   │   │
│   │   └── Special/                    ⭐ Casos especiais
│   │       ├── NamespaceRemovalStrategy.cs
│   │       ├── HtmlNodeStrategy.cs
│   │       └── ...
│   │
│   ├── Registry/
│   │   └── StrategyRegistry.cs         (Registro de estratégias)
│   │
│   └── TypeScriptEmitter.cs
```

---

## 💡 Interfaces e Contratos

### 1. Interface Base: `IConversionStrategy`

```csharp
namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// Estratégia de conversão C# → TypeScript
/// </summary>
public interface IConversionStrategy
{
    /// <summary>
    /// Verifica se esta estratégia pode converter o nó
    /// </summary>
    bool CanConvert(SyntaxNode node, ConversionContext context);

    /// <summary>
    /// Converte o nó para TypeScript
    /// </summary>
    string Convert(SyntaxNode node, ConversionContext context);

    /// <summary>
    /// Prioridade (maior = executado primeiro)
    /// Útil quando múltiplas estratégias podem converter o mesmo tipo
    /// </summary>
    int Priority => 0;
}
```

### 2. Contexto Compartilhado: `ConversionContext`

```csharp
namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// Contexto compartilhado entre estratégias
/// </summary>
public class ConversionContext
{
    public SemanticModel? SemanticModel { get; set; }
    public CSharpToJsConverter Converter { get; set; } // Para conversões recursivas
    public MethodRegistry MethodRegistry { get; set; }

    // Cache para evitar reprocessamento
    private readonly Dictionary<SyntaxNode, string> _cache = new();

    public string? GetCached(SyntaxNode node)
    {
        return _cache.TryGetValue(node, out var result) ? result : null;
    }

    public void SetCached(SyntaxNode node, string result)
    {
        _cache[node] = result;
    }
}
```

---

## 📝 Exemplos de Estratégias

### Exemplo 1: `AnyStrategy.cs` (LINQ)

```csharp
namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Converte LINQ .Any() para JavaScript
/// - Any() sem predicado → length > 0
/// - Any(predicate) → some(predicate)
/// </summary>
public class AnyStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        return memberAccess.Name.Identifier.Text == "Any";
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

        var caller = context.Converter.ConvertExpression(memberAccess.Expression);
        var hasArguments = invocation.ArgumentList.Arguments.Count > 0;

        if (hasArguments)
        {
            // Any(predicate) → some(predicate)
            var predicate = context.Converter.ConvertExpression(
                invocation.ArgumentList.Arguments[0].Expression
            );
            return $"{caller}.some({predicate})";
        }
        else
        {
            // Any() → length > 0
            return $"{caller}.length > 0";
        }
    }

    public int Priority => 10; // Alta prioridade - muito específico
}
```

### Exemplo 2: `EnumStrategy.cs`

```csharp
namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Converte acesso a enums C# para string literals TypeScript
/// - Display.Flex → 'flex'
/// - FlexWrap.Wrap → 'wrap'
/// Heurística: Type.Member onde ambos são PascalCase
/// </summary>
public class EnumStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not MemberAccessExpressionSyntax memberAccess)
            return false;

        var expr = memberAccess.Expression.ToString();
        var member = memberAccess.Name.Identifier.Text;

        // Heurística: Type.Member onde ambos PascalCase
        bool isPascalCase = !expr.Contains('.') &&
                           !expr.StartsWith("this.") &&
                           expr.Length > 0 &&
                           char.IsUpper(expr[0]) &&
                           char.IsUpper(member[0]);

        // Excluir Nullable properties
        if (member == "Value" || member == "HasValue")
            return false;

        return isPascalCase;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)node;
        var member = memberAccess.Name.Identifier.Text;

        // Convert to camelCase string literal
        return $"'{ToCamelCase(member)}'";
    }

    public int Priority => 5; // Média prioridade - heurística

    private string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
```

### Exemplo 3: `NullableStrategy.cs`

```csharp
namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Converte Nullable<T> properties para JavaScript
/// - prop.HasValue → prop != null
/// - prop.Value → prop
/// </summary>
public class NullableStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not MemberAccessExpressionSyntax memberAccess)
            return false;

        var member = memberAccess.Name.Identifier.Text;
        return member == "HasValue" || member == "Value";
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)node;
        var member = memberAccess.Name.Identifier.Text;
        var expr = context.Converter.ConvertExpression(memberAccess.Expression);

        return member switch
        {
            "HasValue" => $"({expr} != null)",
            "Value" => expr,
            _ => throw new InvalidOperationException()
        };
    }

    public int Priority => 15; // Alta prioridade - muito específico
}
```

---

## 🔧 Orquestrador Refatorado: `CSharpToJsConverter.cs`

```csharp
namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// Orquestrador de conversão C# → TypeScript
/// Delega para estratégias especializadas
/// </summary>
public class CSharpToJsConverter
{
    private readonly StrategyRegistry _strategyRegistry;
    private readonly ConversionContext _context;

    public CSharpToJsConverter()
    {
        _context = new ConversionContext { Converter = this };
        _strategyRegistry = new StrategyRegistry();

        // Registrar todas as estratégias
        RegisterStrategies();
    }

    public void SetSemanticModel(SemanticModel? model)
    {
        _context.SemanticModel = model;
    }

    /// <summary>
    /// Converte expressão C# para TypeScript
    /// Usa Strategy Pattern para delegar
    /// </summary>
    public string ConvertExpression(ExpressionSyntax expression)
    {
        // Cache check
        var cached = _context.GetCached(expression);
        if (cached != null) return cached;

        // Encontrar estratégia que pode converter
        var strategy = _strategyRegistry.FindStrategy(expression, _context);

        if (strategy != null)
        {
            var result = strategy.Convert(expression, _context);
            _context.SetCached(expression, result);
            return result;
        }

        // Fallback: retornar texto original
        return expression.ToString();
    }

    public string Convert(SyntaxNode node)
    {
        return node switch
        {
            ExpressionSyntax expr => ConvertExpression(expr),
            StatementSyntax stmt => ConvertStatement(stmt),
            _ => node.ToString()
        };
    }

    private string ConvertStatement(StatementSyntax statement)
    {
        var strategy = _strategyRegistry.FindStrategy(statement, _context);
        return strategy?.Convert(statement, _context) ?? statement.ToString();
    }

    private void RegisterStrategies()
    {
        // LINQ Strategies
        _strategyRegistry.Register<AnyStrategy>();
        _strategyRegistry.Register<SelectStrategy>();
        _strategyRegistry.Register<WhereStrategy>();
        _strategyRegistry.Register<FirstStrategy>();

        // Type Strategies
        _strategyRegistry.Register<NullableStrategy>();
        _strategyRegistry.Register<EnumStrategy>();
        _strategyRegistry.Register<CollectionStrategy>();

        // Expression Strategies
        _strategyRegistry.Register<MemberAccessStrategy>();
        _strategyRegistry.Register<InvocationStrategy>();
        _strategyRegistry.Register<ObjectCreationStrategy>();
        _strategyRegistry.Register<BinaryExpressionStrategy>();

        // Special Strategies
        _strategyRegistry.Register<NamespaceRemovalStrategy>();
        _strategyRegistry.Register<HtmlNodeStrategy>();

        // Fallback (baixa prioridade)
        _strategyRegistry.Register<DefaultExpressionStrategy>();
    }
}
```

---

## 📦 Registro de Estratégias: `StrategyRegistry.cs`

```csharp
namespace eQuantic.UI.Compiler.CodeGen.Registry;

/// <summary>
/// Registro e gerenciamento de estratégias de conversão
/// </summary>
public class StrategyRegistry
{
    private readonly List<IConversionStrategy> _strategies = new();

    /// <summary>
    /// Registra uma estratégia
    /// </summary>
    public void Register<T>() where T : IConversionStrategy, new()
    {
        _strategies.Add(new T());
    }

    /// <summary>
    /// Registra uma instância de estratégia
    /// </summary>
    public void Register(IConversionStrategy strategy)
    {
        _strategies.Add(strategy);
    }

    /// <summary>
    /// Encontra a estratégia com maior prioridade que pode converter o nó
    /// </summary>
    public IConversionStrategy? FindStrategy(SyntaxNode node, ConversionContext context)
    {
        return _strategies
            .Where(s => s.CanConvert(node, context))
            .OrderByDescending(s => s.Priority)
            .FirstOrDefault();
    }
}
```

---

## 🧪 Testabilidade

### Antes (Difícil de testar)

```csharp
[Fact]
public void TestAnyConversion()
{
    var converter = new CSharpToJsConverter();
    // Precisa criar SyntaxTree completo
    // Testa classe gigante inteira
}
```

### Depois (Fácil de testar)

```csharp
[Fact]
public void AnyWithoutPredicate_ConvertsToLengthCheck()
{
    // Arrange
    var strategy = new AnyStrategy();
    var context = CreateTestContext();
    var code = "items.Any()";
    var syntax = ParseExpression(code);

    // Act
    var result = strategy.Convert(syntax, context);

    // Assert
    Assert.Equal("items.length > 0", result);
}

[Fact]
public void AnyWithPredicate_ConvertsToSome()
{
    // Arrange
    var strategy = new AnyStrategy();
    var context = CreateTestContext();
    var code = "items.Any(x => x > 5)";
    var syntax = ParseExpression(code);

    // Act
    var result = strategy.Convert(syntax, context);

    // Assert
    Assert.Contains(".some(", result);
}
```

---

## 📈 Plano de Migração (Faseado)

### Fase 1: Infraestrutura (Sprint 1)
- [ ] Criar `IConversionStrategy` interface
- [ ] Criar `ConversionContext`
- [ ] Criar `StrategyRegistry`
- [ ] Refatorar `CSharpToJsConverter` para usar registry
- [ ] Testes unitários da infraestrutura

### Fase 2: Estratégias Críticas (Sprint 2)
- [ ] Migrar `AnyStrategy`
- [ ] Migrar `EnumStrategy`
- [ ] Migrar `NullableStrategy`
- [ ] Migrar `NamespaceRemovalStrategy`
- [ ] Testes para cada estratégia

### Fase 3: LINQ Strategies (Sprint 3)
- [ ] `SelectStrategy`
- [ ] `WhereStrategy`
- [ ] `FirstStrategy`
- [ ] `AllStrategy`
- [ ] Testes

### Fase 4: Expression Strategies (Sprint 4)
- [ ] `MemberAccessStrategy`
- [ ] `InvocationStrategy`
- [ ] `ObjectCreationStrategy`
- [ ] `BinaryExpressionStrategy`
- [ ] Testes

### Fase 5: Deprecação (Sprint 5)
- [ ] Remover código antigo de `CSharpToJsConverter`
- [ ] Documentação
- [ ] Performance benchmarks

---

## ✅ Benefícios

### 1. **Extensibilidade**
```csharp
// Adicionar nova conversão = criar nova estratégia
public class TupleStrategy : IConversionStrategy { ... }

// Registrar
_strategyRegistry.Register<TupleStrategy>();

// DONE! Sem modificar código existente (Open/Closed Principle)
```

### 2. **Manutenibilidade**
- Cada estratégia: 50-100 linhas (vs 700+ linhas monolítico)
- Mudança isolada não afeta outras conversões
- Fácil encontrar código relevante

### 3. **Testabilidade**
- Testes unitários focados em uma conversão
- Mock de dependências fácil
- Coverage mais alto

### 4. **Escalabilidade**
- Adicionar 50 novas conversões = 50 arquivos pequenos
- Não cresce descontroladamente
- Desenvolvedores trabalham em paralelo sem conflitos

### 5. **Flexibilidade**
- Prioridade permite resolver conflitos
- Estratégias podem ser compostas
- Fácil adicionar logging/debugging por estratégia

---

## 🎓 Princípios Aplicados

### ✅ SOLID

1. **Single Responsibility Principle (SRP)**
   - Cada estratégia tem UMA responsabilidade: converter um tipo específico

2. **Open/Closed Principle (OCP)**
   - Aberto para extensão (novas estratégias)
   - Fechado para modificação (registry não muda)

3. **Liskov Substitution Principle (LSP)**
   - Todas as estratégias implementam `IConversionStrategy`
   - Substituíveis entre si

4. **Interface Segregation Principle (ISP)**
   - Interface mínima e focada
   - Apenas `CanConvert` e `Convert`

5. **Dependency Inversion Principle (DIP)**
   - `CSharpToJsConverter` depende de `IConversionStrategy` (abstração)
   - Não depende de implementações concretas

### ✅ Design Patterns

1. **Strategy Pattern**
   - Algoritmos encapsulados e intercambiáveis

2. **Chain of Responsibility** (implícito)
   - Registry percorre estratégias até encontrar uma que pode converter

3. **Registry Pattern**
   - Registro centralizado de estratégias

---

## 📊 Comparação: Antes vs Depois

| Aspecto | Antes | Depois |
|---------|-------|--------|
| **Linhas por arquivo** | 700+ | 50-100 |
| **Adicionar conversão** | Modificar classe grande | Criar nova estratégia |
| **Testes** | Classe completa | Estratégia isolada |
| **Conflitos Git** | Frequentes (arquivo único) | Raros (arquivos separados) |
| **Compreensão** | Difícil (muito código) | Fácil (arquivo pequeno) |
| **Manutenção** | Alto risco | Baixo risco (isolado) |

---

## 🚀 Próximos Passos

1. **Aprovação do plano**
   - Review da arquitetura proposta
   - Ajustes se necessário

2. **Implementação Fase 1**
   - Criar infraestrutura base
   - Testes da infraestrutura

3. **Migração incremental**
   - Migrar estratégias uma por vez
   - Manter compatibilidade durante migração

4. **Documentação**
   - Como adicionar nova estratégia
   - Exemplos de uso

---

**Data de criação:** 2026-01-24
**Autor:** Claude (Anthropic)
**Status:** 📋 Proposta - Aguardando aprovação
