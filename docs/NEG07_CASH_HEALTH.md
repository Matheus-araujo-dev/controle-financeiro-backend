# NEG-07 — Saúde e previsão de caixa

Leitura do fluxo oficial do mês: primeiro saldo negativo, menor saldo, saldo final, variação e composição diária. Escopo consolidado de todas as contas, identificado na interface. Não equivale à conciliação nem garante completude dos cadastros. Links abrem registros do período; previsões ainda não materializadas não têm lançamento navegável.

TDD reproduziu saldo parcial superestimado (100 em vez de 60) e fatura contada duas vezes (600 em vez de 300). O backend corrige esses casos e compartilha a regra de obrigação consolidada com o resumo. Sem migration ou alteração de DTO/schema. O frontend recusa dados de outro mês, série incompleta e erro como diagnóstico de saúde.

Validação focada: 26 testes de dashboard backend e 155 de dashboard frontend. Gates completos/publicação pendentes. Rollback: revert dos commits de implementação; baselines backend 95978d2948d7b3ac28b25a08d446a93a017a8bca e frontend 9e8af3fad3c4690e4e300f41c17b7203bf746e52. Nenhuma alteração de dados. Backend deve ser publicado antes da interface.

Revisão final: teste adicional reproduziu importação materializada somada em duplicidade (200 em vez de 100). O fluxo passa a usar a conta materializada para o evento conhecido; a informação importada permanece disponível como semente de projeções futuras. Correção sem alteração de schema público. Dez E2E aprovados; build de produção aprovado em desktop/mobile/standalone emulado com APIs simuladas. Suíte local backend inicial: 913 aprovados, seis ignorados por dependências/provider indisponíveis, cobertura 80,6%; gate será repetido após a correção adicional. Frontend: 1.425 testes, cobertura 85,60% statements, 80,39% branches, 82,07% functions e 87,78% lines.

Gate final local aprovado após a regressão adicional: cobertura de linhas 80,6%, nenhum pacote vulnerável. Testes que dependem de runtime OCR/provider indisponível foram ignorados localmente e serão cobertos pelo ambiente CI PostgreSQL/OCR. Produção ainda pendente.
