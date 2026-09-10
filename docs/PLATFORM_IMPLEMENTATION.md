# Atualização de plataforma .NET 10 LTS

.NET 9 encerra suporte em novembro de 2026. Esta etapa atualiza os projetos para
net10.0, pacotes Microsoft/EF Core para 10.0.12, Npgsql EF para 10.0.3 e imagens/CI
para 10.0. A referência Microsoft.AspNetCore.OpenApi não utilizada foi removida;
Swagger continua provido pelo Swashbuckle e preserva as mesmas rotas/contratos.

## Integração e validação local — 2026-09-10

A branch `codex/platform-net10` integra o core de segurança, regras financeiras e
contrato OpenAPI `29f814c` com a atualização de plataforma `3f1f022`. Merge local:
`74d66ab`, sem conflitos. A worktree isolada preserva o backend principal.

- Build: zero avisos e erros.
- Testes: 244 domínio, 96 aplicação, 67 infraestrutura e 382 API aprovados;
  3 testes existentes dependem de PostgreSQL e foram pulados no fallback SQLite.
- Gate completo: `scripts/coverage-gate.ps1 -Threshold 80` aprovado com 80,5% de
  cobertura de linhas combinada, 63,4% de branches e 81% na API.
- O snapshot não apresenta mudanças pendentes no EF Core 10; o teste de geração
  do script PostgreSQL da migration de recuperação de proprietários passou.
- Testes de isolamento, refresh concorrente, jobs, recuperação conservadora de
  dados legados, ciclo de fatura e contrato OpenAPI passam na composição .NET 10.
- Evidências locais: `platform-build.log`, `platform-coverage-gate.log` e
  `TestResults/coverage-report/Summary.txt` na worktree.

## Promoção

Validação local concluída; esta etapa ainda não está concluída em entrega.
Nenhum push foi realizado pelo agente desta worktree. Promover somente após
pipeline de develop verde e, em seguida, pipeline de main verde, registrando os
SHAs e runs. A validação PostgreSQL completa permanece obrigatória no CI.
