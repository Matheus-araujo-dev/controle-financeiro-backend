# Conciliação de fatura — entrega e reversão

Implementação na branch codex/conciliacao-fatura-20260914, em worktrees isolados.

## Comportamento

Revisão do PDF vincula contas existentes ou cria somente itens ausentes. O valor confirmado do PDF substitui apenas a parcela vinculada com aceite explícito e auditoria do valor anterior. Metadados da conta existente prevalecem. Rascunhos não criam contas; decisões confirmadas alimentam memória por família, cartão e estabelecimento. Reembolso e recorrência reutilizam os serviços financeiros atuais. A prévia de reembolso não grava dados.

## Validação local de 15/09

- Backend integrado: 886 testes aprovados, 3 ignorados no fallback SQLite; cobertura consolidada 80,5% de linhas; build Release sem erros/avisos; EF sem alterações de modelo pendentes.
- Frontend integrado: 1.375 testes aprovados, 87,64% linhas e 80,12% branches; lint sem erros e build aprovado. Ajustes posteriores de largura da grade passam por validação dirigida antes do push.
- Contrato OpenAPI exportado e tipos derivados do Swagger. Compatibilidade bancária OFX/CSV coberta por testes HTTP; adapter frontend alinhado à lista, datas e sugestões reais.
- Inspeção local com dados sintéticos em Edge; campos editáveis preservam 250px e preenchimento em lote respeita quebra responsiva. Nenhum lançamento real usado ou alterado.

## Promoção

Primeiro PR e CI em develop, incluindo PostgreSQL. Somente após o commit de develop estar verde, promover para main e aguardar CI e build/deploy Railway. Validação local não constitui publicação.

## Reversão

- Checkpoints locais anteriores à integração: backend b3fa7c2, frontend cf9b374. Bases de develop antes da entrega: backend 0bf188a, frontend e9863c5.
- Em problema de interface, reverter o commit da entrega no frontend por novo PR, mantendo o backend compatível e seus filtros de sessões bancárias. Isso interrompe acesso ao fluxo novo sem apagar contas ou memória.
- Não executar migrations Down em produção e não excluir sessões: preservam decisões, rastreabilidade e vínculos financeiros. Migrations novas são aditivas.
- Rollback de backend deve preservar as colunas e os filtros FaturaId no serviço bancário; restaurar cegamente versão antiga é incompatível com sessões de cartão já gravadas. Usar correção reversiva que desabilite os endpoints novos mantendo leitura e isolamento das sessões.
- Uma conta/reembolso já confirmado é dado financeiro: sua reversão usa os fluxos de cancelamento e edição existentes, com revisão humana, e não uma remoção em massa de banco.

## Limites

A prévia simula divisão e datas; a confirmação aplica todas as validações de negócio. Importação cria somente a parcela constante no PDF: acompanhar parcelas no reembolso usa apenas parcelas realmente cadastradas no grupo. O backend bancário existente lista as 50 revisões mais recentes; limite mostrado no frontend.
