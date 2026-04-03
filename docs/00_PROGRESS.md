# Progress Log - Backend

## Ultima fase concluida
- Fase 1: fundacao tecnica do backend concluida com auth preparada, filtros/paginacao base, endpoints tecnicos, build, testes e coverage.

## Decisoes locais
- .NET 9 foi adotado porque ja esta disponivel no ambiente e a documentacao permite .NET 9 ou LTS vigente.
- A auditoria inicial foi materializada com `AuditTrailEntry` e stamping base em `AuditableEntity`, sem adiantar regras financeiras.
- A fundacao tecnica ficou restrita a health check, Swagger, contratos comuns, DbContext, auth base, filtros/paginacao e pipeline de erro padronizado.
- O build local precisa usar `-m:1` para evitar falhas silenciosas de paralelismo no ambiente atual.
- O endpoint protegido inicial usa o modo `Development` com header `X-Debug-User`, suficiente para preparar a autenticacao sem acoplar regra de negocio.

## Pendencias nao criticas
- configurar secrets reais de SonarQube/SonarCloud no CI para ativar o quality gate remoto.
- evoluir a auditoria de before/after quando os modulos financeiros surgirem.
