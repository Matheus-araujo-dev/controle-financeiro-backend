# Segurança de isolamento e renovação de sessão — 2026-09-09

## Necessidade

O filtro global de dados financeiros tratava ausência de workspace como acesso a todos os dados. A consulta de compras importadas do dashboard também incluía itens legados com `FamiliaId = Guid.Empty` para qualquer usuário. Na rotação de refresh token, dois contextos podiam consumir o mesmo token ainda ativo e persistir dois sucessores.

Essas condições são incompatíveis com isolamento entre workspaces. A correção deve preservar o funcionamento dos jobs e webhooks sem transformar ausência de identidade em autorização global.

## Implementação

- `AppDbContext`: consultas de entidades tenant retornam zero linhas quando o workspace é ausente ou vazio. Workspace explícito continua limitando pelo `FamiliaId`.
- Jobs de recorrência, atualização de status, transição de futuro e alertas email/push/WhatsApp enumeram o cadastro de famílias e executam cada workspace num escopo DI/DbContext separado. Falha em um workspace é registrada e não contamina o change tracker do próximo; cancelamento é propagado.
- Webhooks autenticados conservam um lookup pontual deliberadamente global pelo telefone normalizado e ativo. Após resolver o proprietário, definem o workspace antes das consultas financeiras/conversas.
- Cache de lookups conserva o workspace explícito ao abrir scopes internos e separa entradas por workspace, inclusive quando um mesmo scope troca de workspace.
- Dashboard volta a respeitar o filtro global e não interpreta registros sem proprietário como dados compartilhados.
- Refresh usa a coluna existente `RevogadoEmUtc` como token de concorrência. O UPDATE só vence se a revogação original continuar inalterada. A transação de SaveChanges reverte o sucessor perdedor; o serviço traduz o conflito para falha de autenticação e limpa o tracker.

## Compatibilidade e dados legados

Não há alteração em DTOs, rotas ou payloads de API. A proteção de refresh não acrescenta coluna. A migration de dados recupera somente itens sem workspace cujo registro pai aponta para uma família cadastrada. Itens já atribuídos não são reescritos. Registros sem evidência de propriedade continuam preservados no banco e indisponíveis em consultas operacionais; atribuí-los exige identificação do proprietário, sem suposição automática. Rollback não remove atribuições recuperadas.

Fixtures que acessavam serviços diretamente sem contexto HTTP passam a definir explicitamente o workspace de desenvolvimento; as asserções funcionais originais são preservadas. Testes dedicados continuam usando contexto anônimo para comprovar isolamento fechado.

## Validação

- TDD inicial confirmado: 2 testes novos falharam na implementação anterior (leitura sem workspace e corrida de refresh).
- Os mesmos 2 testes passaram após as correções iniciais.
- Testes adicionais: execução isolada de jobs, continuidade após falha, cancelamento, tratamento de conflito no serviço de autenticação, troca de workspace no cache e recuperação conservadora/idempotente de dados legados.
- Infraestrutura: 67/67 testes verdes; snapshot PostgreSQL sem mudanças pendentes e script da migration gerado com sucesso.
- Importações: 20/20 testes de controller verdes após fixtures explícitas.
- Migration: teste RED falhou sem o SQL; GREEN confirmou recuperação pelo pai, preservação de itens atribuídos/quarentena e idempotência em SQLite.
- Gate completo executado em 2026-09-09: aprovado com 80,5% de cobertura de linhas combinada (anterior: 80,1%), 63,4% de branches e 81% na API. As quatro suítes passaram, sem reduzir threshold, exclusões ou asserções. Relatório: `TestResults/coverage-report/Summary.txt`; execução: `scripts/coverage-gate.ps1 -Threshold 80`.
- Entrega develop → CI verde → main → CI verde: responsabilidade do coordenador; esta etapa só estará concluída após as duas pipelines.
