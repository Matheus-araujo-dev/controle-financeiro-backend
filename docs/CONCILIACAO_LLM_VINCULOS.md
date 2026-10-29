# Conciliação de fatura com sugestões de vínculo por LLM — 2026-10-29

Ao iniciar a revisão de um PDF, a LLM recebe os itens pendentes, as contas elegíveis do mesmo cartão/ciclo e as preferências confirmadas. Retorna campos, conta sugerida e justificativa. IDs são validados contra as contas autorizadas, sinal e parcela; a sugestão não confirma vínculos nem alterações de valor.

A seleção de contas passa a usar o ciclo de fechamento/vencimento, respeitando vínculos explícitos e excluindo contas canceladas ou vinculadas a outra fatura. Consulta, confirmação e prévia de reembolso compartilham esse critério. Upload não altera o total financeiro; confirmação continua sendo necessária. Reanexo preserva rascunhos e só analisa pendências sem rascunho.

Contrato aditivo: SugestaoItemConciliacaoResponse ganha contaPagarId e justificativa opcionais. Sem migrations. Sugestões ficam no JSON de rascunho existente e são revalidadas ao carregar a revisão.

Validação: regressão reproduzida antes da correção (vencimento 15 versus 20 do mesmo ciclo); 37 testes de aplicação de conciliação; integração com LLM simulada (sucesso e indisponibilidade), isolamento de ciclo, confirmação explícita e nenhuma alteração financeira no upload. Suíte completa: 1.095 aprovados, 6 ignorados, usando SQLite; cobertura merged de linhas 80,4%. PostgreSQL, provedor LLM real e produção ainda não validados.

Base isolada: 1f3a7d3 (main anterior à refatoração de responsáveis). main c7aaf82 / develop c7c795b têm falhas preexistentes de compilação: GrupoPagadoresInfo ausente; referências antigas a ResponsavelCompraId/ResponsavelId e assinaturas incompatíveis. A entrega fica em rascunho até integrar com essa refatoração e obter develop e main verdes.

Rollback: reverter o commit deste ajuste e publicar o artefato anterior verificado. Não apagar sessões nem contas; os novos campos são opcionais e os rascunhos continuam compatíveis. Reversão não foi exercitada em produção. Não há migração de banco nem alteração de credenciais.

