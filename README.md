# Backend - Controle Financeiro

Bootstrap inicial do backend do sistema de controle financeiro, alinhado a `Fase 0` do workspace.

## Stack

- .NET 9
- ASP.NET Core Web API
- EF Core + SQL Server
- Swagger
- xUnit + FluentAssertions
- Coverlet + Sonar

## Estrutura

```text
src/
  ControleFinanceiro.Api
  ControleFinanceiro.Application
  ControleFinanceiro.Contracts
  ControleFinanceiro.Domain
  ControleFinanceiro.Infrastructure
  ControleFinanceiro.SharedKernel
tests/
  ControleFinanceiro.Api.Tests
  ControleFinanceiro.Application.Tests
  ControleFinanceiro.Domain.Tests
  ControleFinanceiro.Infrastructure.Tests
```

## Comandos

```powershell
dotnet restore
dotnet build --configuration Release -m:1
dotnet test --configuration Release -m:1 /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
dotnet ef database update --project src\ControleFinanceiro.Infrastructure --startup-project src\ControleFinanceiro.Api
dotnet run --project src\ControleFinanceiro.Api
```

Swagger: `http://localhost:5000/swagger`

Health: `http://localhost:5000/health`

## Qualidade

- cobertura OpenCover gerada em `tests/*/coverage.opencover.xml`
- workflow próprio em `.github/workflows/ci.yml`
- Sonar preparado via `SONAR_TOKEN` e `SONAR_HOST_URL`

## Configuração local

- ajustar `ConnectionStrings:SqlServer` em `src/ControleFinanceiro.Api/appsettings.json` ou `appsettings.Local.json`
- a migration inicial do bootstrap já está versionada no repositório
