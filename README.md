# Backend - Controle Financeiro

Backend do sistema de controle financeiro, alinhado ate a `Fase 2` do workspace.

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

Endpoints estruturais:

- `GET /api/v1/bootstrap/status`
- `GET /api/v1/bootstrap/modules`
- `POST /api/v1/bootstrap/echo`
- `GET /api/v1/security/me`

Cadastros de apoio:

- `GET|POST|PUT /api/v1/pessoas`
- `PATCH /api/v1/pessoas/{id}/ativar`
- `PATCH /api/v1/pessoas/{id}/inativar`
- `GET|POST|PUT /api/v1/formas-pagamento`
- `GET|POST|PUT /api/v1/contas-bancarias`
- `GET|POST|PUT /api/v1/cartoes`
- `GET|POST|PUT /api/v1/contas-gerenciais`

## Qualidade

- cobertura OpenCover gerada em `tests/*/coverage.opencover.xml`
- workflow próprio em `.github/workflows/ci.yml`
- Sonar preparado via `SONAR_TOKEN` e `SONAR_HOST_URL`

## Escopo desta entrega

- camadas base completas
- auth preparada com modo `Development` via header `X-Debug-User`
- tratamento de erro padronizado
- filtros e paginacao estruturais
- auditoria base e migration inicial
- endpoints tecnicos para smoke e integracao inicial

## Configuração local

- ajustar `ConnectionStrings:SqlServer` em `src/ControleFinanceiro.Api/appsettings.json` ou `appsettings.Local.json`
- a migration inicial do bootstrap já está versionada no repositório
