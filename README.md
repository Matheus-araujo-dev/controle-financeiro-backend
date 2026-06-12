# Backend - Controle Financeiro

[![CI](https://github.com/anomalyco/controle-financeiro/actions/workflows/backend-ci.yml/badge.svg)](https://github.com/anomalyco/controle-financeiro/actions/workflows/backend-ci.yml)
[![Coverage](https://codecov.io/gh/anomalyco/controle-financeiro/branch/main/graph/badge.svg?flag=backend)](https://codecov.io/gh/anomalyco/controle-financeiro)

## Stack

| Categoria | Tecnologia |
|-----------|------------|
| Framework | ASP.NET Core 9 |
| ORM | EF Core 9 |
| Banco | SQL Server |
| API | OpenAPI/Swagger |
| Arquitetura | Clean Architecture |
| Testes | xUnit + FluentAssertions |

## Scripts

```bash
dotnet restore          # Restaurar dependências
dotnet build          # Compilar solução
dotnet test           # Executar testes
dotnet coverage       # Gerar coverage
dotnet run --project src/ControleFinanceiro.Api  # Executar API
dotnet ef migrations list # Listar migrations
dotnet ef migrations add <Nome> # Nova migration
dotnet ef database update   # Aplicar migrations
```

## Estrutura

```
src/
├── ControleFinanceiro.Api/        # Web API
├── ControleFinanceiro.Application/ # Application Services
├── ControleFinanceiro.Contracts/    # DTOs e kontratos
├── ControleFinanceiro.Domain/    # Entidades e regras
├── ControleFinanceiro.Infrastructure/ # Persistência
└── ControleFinanceiro.SharedKernel/  # Abstrações compartilhadas

tests/
├── ControleFinanceiro.Api.Tests/
├── ControleFinanceiro.Application.Tests/
├── ControleFinanceiro.Domain.Tests/
└── ControleFinanceiro.Infrastructure.Tests/
```

## Execucao Local

```bash
dotnet restore
dotnet run --project src/ControleFinanceiro.Api
```

Configuracao:
- `appsettings.Local.json` - Configurações locais
- Connection string: `CFLocalDB` (SQL Server local)

URLs:
- API: http://localhost:5000
- Swagger: http://localhost:5000/swagger/v1
- Health: http://localhost:5000/health

## Quality Gate

- Coverage mínimo: 80%
- SonarQube integrado
- dotnet list package --vulnerable para dependências