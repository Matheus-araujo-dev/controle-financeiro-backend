# Progress Log - Backend

## Ultima fase concluida
- Fase 0: bootstrap estrutural do backend concluido com build, testes, coverage e migration inicial.

## Decisoes locais
- .NET 9 foi adotado porque ja esta disponivel no ambiente e a documentacao permite .NET 9 ou LTS vigente.
- A auditoria inicial foi materializada com `AuditTrailEntry` e stamping base em `AuditableEntity`, sem adiantar regras financeiras.
- O bootstrap ficou restrito a health check, Swagger, contratos comuns, DbContext e pipeline de erro padronizado.
- O build local precisa usar `-m:1` para evitar falhas silenciosas de paralelismo no ambiente atual.

## Pendencias nao criticas
- configurar secrets reais de SonarQube/SonarCloud no CI para ativar o quality gate remoto.
- evoluir a auditoria de before/after quando os modulos financeiros surgirem.
