# Fechamento do MVP - Backend

## Status

MVP fechado no backend ate a fase 9, sem pendencias estruturais criticas abertas.

## Checklist da fase 9

- README atualizado
- documentacao local minima mantida
- build local verde
- testes verdes
- coverage gerada
- Sonar preparado com quality gate aguardado no CI quando secrets estiverem configurados
- worktree limpo ao final da fase

## Validacao local usada no fechamento

```powershell
dotnet build --configuration Release -m:1 --no-restore
# Gate merged de cobertura (coverlet.collector + ReportGenerator), threshold 80%:
./scripts/coverage-gate.ps1 -Threshold 80
```

## Artefatos principais

- cobertura (cobertura + opencover): `TestResults/coverage-collector/**/coverage.*.xml`
- relatório merged: `TestResults/coverage-report/` (gerado pelo ReportGenerator)
- pipeline: `.github/workflows/ci.yml`
- configuracao Sonar: `sonar-project.properties`

## Observacoes

- o gate remoto depende de `SONAR_TOKEN` e `SONAR_HOST_URL`
- o comando local continua usando `-m:1` para evitar instabilidade de paralelismo no ambiente atual
