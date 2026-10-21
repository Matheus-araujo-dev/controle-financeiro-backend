# Ciclo de recorrência — 2026-09-12

Implementação: identidade mensal de cartão evita duplicação; cartão permanece EM_FATURA; demais formas preservam FUTURO/PENDENTE. Pausa e retomada preservam IDs com marca persistida da causa. Encerramento manual exige pausa e é definitivo; o worker encerra regras ao atingir data fim.

TDD: regressões de status, duplicação, janela e encerramento observadas falhando antes das respectivas alterações. Suíte global: 803 aprovados, 3 ignorados; cobertura consolidada 80,7%. Release sem erros ou avisos. A validação usa SQLite de fallback; não representa execução de PostgreSQL em produção.

Migration RecorrenciaEncerramentoEPausa adiciona três booleanos com default false e GerarAPartirDe (date nullable), que impede geração retroativa após pausa longa: Encerrada na regra e CanceladaPorPausaRecorrencia nas contas a pagar/receber. Nenhum lançamento histórico é removido ou reclassificado. Modelo sem alterações pendentes.

Reversão: antes de publicar, guardar backup do banco e os SHAs de deploy. Reverter primeiro a aplicação para a versão anterior mantendo as colunas aditivas (compatíveis). Se for necessário remover o schema, exportar antes os quatro campos e executar Down da migration após reverter a aplicação; Down perde a marca de pausa e encerramento. Não executar Down em produção automaticamente. Não houve deploy ou alteração de dados de produção nesta execução.
