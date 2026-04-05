# Backend - Controle Financeiro

Backend do sistema de controle financeiro, consolidado no fechamento do MVP e alinhado ate a `Fase 9` do workspace.

## Status do MVP

Entregas concluídas no backend:

- fundacao tecnica com `Api`, `Application`, `Domain`, `Infrastructure`, `Contracts` e `SharedKernel`
- cadastros de apoio: pessoas, formas de pagamento, contas bancarias, cartoes e contas gerenciais
- nucleo financeiro: contas a pagar, contas a receber, rateio, liquidacao, cancelamento e movimentacoes
- cartoes e faturas com visao economica versus caixa
- recorrencia com gerar ocorrencias, pausar, encerrar e alterar futuras
- dashboard executivo e fluxo de caixa
- importacoes WhatsApp/OCR com revisao humana
- conciliacao manual assistida entre extrato importado e movimentacao bancaria

## Stack

- .NET 9
- ASP.NET Core Web API
- EF Core + SQL Server
- Swagger/OpenAPI
- xUnit + FluentAssertions
- Coverlet + SonarQube/SonarCloud

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
docs/
  00_PROGRESS.md
  00_BOOTSTRAP_BACKEND.md
  01_ESTRATEGIA_DE_TESTES_BACKEND.md
  02_MVP_FECHAMENTO.md
```

## Modulos entregues

- `Bootstrap` e `Security` para smoke, health e auth base em desenvolvimento
- `Pessoas`
- `FormasPagamento`
- `ContasBancarias`
- `Cartoes`
- `ContasGerenciais`
- `ContasPagar`
- `ContasReceber`
- `Movimentacoes`
- `Faturas`
- `Dashboard`
- `ImportacoesWhatsApp`
- `Conciliacao`

## Como rodar

```powershell
dotnet restore
dotnet build --configuration Release -m:1
dotnet test --configuration Release -m:1 /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
dotnet ef database update --project src\ControleFinanceiro.Infrastructure --startup-project src\ControleFinanceiro.Api
dotnet run --project src\ControleFinanceiro.Api
```

Endpoints locais:

- Swagger: `http://localhost:5000/swagger`
- Health: `http://localhost:5000/health`

## Configuracao local

- ajustar `ConnectionStrings:SqlServer` em `src/ControleFinanceiro.Api/appsettings.json` ou `appsettings.Local.json`
- a autenticacao de desenvolvimento usa header `X-Debug-User`
- as migrations ja estao versionadas no repositorio

## Qualidade e quality gate

- cobertura OpenCover gerada em `tests/*/TestResults/coverage/coverage.opencover.xml`
- workflow proprio em [`.github/workflows/ci.yml`](D:\Projetos\controle-financeiro\backend\.github\workflows\ci.yml)
- Sonar preparado em [`sonar-project.properties`](D:\Projetos\controle-financeiro\backend\sonar-project.properties)
- o workflow aguarda o quality gate com `sonar.qualitygate.wait=true` quando `SONAR_TOKEN` e `SONAR_HOST_URL` estiverem configurados

## Validacao local recomendada

```powershell
dotnet build --configuration Release -m:1 --no-restore
dotnet test --configuration Release -m:1 --no-build --no-restore /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## Documentacao local

- progresso e decisoes: [`docs/00_PROGRESS.md`](D:\Projetos\controle-financeiro\backend\docs\00_PROGRESS.md)
- fechamento do MVP: [`docs/02_MVP_FECHAMENTO.md`](D:\Projetos\controle-financeiro\backend\docs\02_MVP_FECHAMENTO.md)

## Pendencias nao criticas

- configurar secrets reais de SonarQube/SonarCloud no CI para transformar o quality gate remoto em bloqueio efetivo
- ampliar cobertura seletiva de `Application` e `Infrastructure` conforme a base evoluir alem do MVP
