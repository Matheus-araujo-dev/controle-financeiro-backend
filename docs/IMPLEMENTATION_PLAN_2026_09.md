# Plano de endurecimento e qualidade — setembro de 2026

## Objetivo e autorização

Implementar os achados da revisão de 08/09/2026 sem regressões, mantendo cobertura
mínima de 80% e os repositórios backend/frontend independentes. O usuário autorizou
implementação por agentes e promoção de cada entrega: develop verde, merge em main,
main verde. Nenhum item é concluído apenas por testes locais ou abertura de PR.

## Critérios comuns

- TDD para comportamentos relevantes, incluindo casos adversos e fluxos existentes.
- Não reduzir thresholds, excluir código de produto da cobertura ou ignorar falhas.
- Compilação, testes, cobertura e pipeline precisam passar para o SHA entregue.
- Mudanças de contrato atualizam OpenAPI, consumidores e testes juntos.
- Toda entrega registra commits, PRs, execuções e evidências nos repositórios.
- Falha na pipeline interrompe a promoção; corrigir e repetir antes de main.

## Atividades

| ID | Necessidade | Implementação/aceite | Estado |
| --- | --- | --- | --- |
| SEC-01 | Contexto sem workspace abre consultas globais | Tenant obrigatório; workers com escopo explícito; testes multi-tenant | Em implementação |
| SEC-02 | Dashboard admite importações de família vazia | Remover acesso implícito a legado e preservar dados por migração/escopo seguro | Em implementação |
| SEC-03 | API autenticada persiste em cache PWA compartilhado | Remover cache financeiro e limpar cache legado na atualização | Em implementação |
| SEC-04 | Logout/troca deixa consultas e respostas antigas | Isolar cache por sessão/workspace e cancelar requisições anteriores | Em implementação |
| SEC-05 | Refresh concorrente pode emitir duas sessões | Consumo atômico, teste de concorrência e resposta compatível | Em implementação |
| SEC-06 | CSP de produção permite eval e origens de desenvolvimento | Política restrita preservando OAuth e ambiente local | Em implementação |
| REP-01 | Até 14 fontes carregadas em conjunto | Queries por aba/fonte, debounce, erros independentes e regressão de filtros | Em implementação |
| REP-02 | Primeira página de 250 itens trunca relatórios | Paginação completa/totalização consistente e exportação sem omissões | Em implementação |
| FIN-01 | Total a pagar pode incluir itens EmFatura | Reproduzir e corrigir duplicidade com teste fechamento/pagamento | Pendente |
| PERF-01 | Dashboard materializa referências/histórico amplo | Consultas restritas e projeções sem mudança de resultado | Pendente |
| UI-01 | Estado/filtros de relatório pouco rastreáveis | URL de aba/filtros e estados claros; regressão de navegação e acessibilidade | Em implementação |
| ARCH-01 | Componentes grandes e limites de domínio inconsistentes | Extrair responsabilidades nos fluxos alterados sem nova arquitetura distribuída | Em implementação |
| API-01 | Tipos gerados exigidos mas ausentes | OpenAPI reproduzível, geração versionada, integração de types e verificação CI | Pendente |
| CI-01 | Sonar sem env/cobertura no job e instalação variável | Corrigir escopo/artifact, npm ci e manter gate local obrigatório | Em implementação |
| DOC-01 | Documentação normativa contradiz implementação | Atualizar arquitetura, decisões e operação vigente | Em implementação |
| PLATFORM-01 | .NET 9 encerra suporte em novembro/2026 | Atualizar .NET 10 LTS, imagens/CI/pacotes compatíveis e validar migrations | Pendente |

## Ordem e limites

Priorizar isolamento/cache, consistência financeira/relatórios, contratos/CI,
otimização e plataforma. Medições de performance não serão inventadas. Mudanças
de índices dependem de evidências de consultas; preservar o monólito modular.
Não alterar credenciais, dados de produção ou configuração OAuth sem necessidade
concreta. Uma limitação externa será registrada como pendência, nunca como concluída.

## Base verificada antes da implementação

- Backend: build passou; 244 testes de domínio passaram.
- Frontend: TypeScript passou; suíte completa e cobertura serão executadas na entrega.
- Backend iniciou em develop (24ee30c); frontend em fix/test-stability-coverage-gates
  (434258d, uma correção de testes à frente de origin/develop).
