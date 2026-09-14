# FIN-01 / PERF-01 — Resumo financeiro e consultas auxiliares

## Necessidade e reprodução

Ao fechar uma fatura cujo cartão possui recebedor e forma de pagamento padrão, o sistema cria uma conta a pagar consolidada. Os itens originais permanecem com status `EmFatura`. O resumo somava ambas as representações da mesma obrigação: compra de R$ 300 gerava `totalAPagar = 600` após fechamento.

A reprodução TDD em `DashboardFinancialIntegrityTests` confirmou esperado 300 / recebido 600 em dois cenários: vencimento no mesmo mês da competência e no mês seguinte. Sem conta consolidada, o ciclo já retornava 300 corretamente; por isso remover `EmFatura` incondicionalmente seria regressão.

## Correção

`DashboardResumoService` soma itens `EmFatura` enquanto não existir obrigação consolidada não cancelada para a fatura. Quando ela existe, somente a obrigação consolidada entra no total. A associação considera o vínculo explícito à fatura e o cartão/mês de vencimento usado na sincronização para compras manuais sem `FaturaCartaoId`.

O teste cobre compra 300 → fechar 300 → pagar 0 → estornar 300 → reabrir fechamento 300, com e sem geração de conta consolidada, e competência cujo pagamento ocorre no mês seguinte. Não houve alteração em contratos HTTP ou schema. A correção adicional de reabertura descrita abaixo preserva os registros históricos.

## Reabertura e preservação do histórico

O ciclo completo revelou um segundo defeito: reabrir o fechamento após estornar pagamento retornava HTTP 500. A implementação apagava a conta consolidada ainda referenciada por movimentações estornadas (`DeleteBehavior.Restrict`). `FaturaCartaoAppService.ReabrirAsync` agora cancela essa obrigação, preservando sua identidade e referências. `FecharAsync` ignora obrigações canceladas na verificação de existência, criando nova consolidação quando necessário.

A regressão verifica obrigação anterior cancelada, movimentação histórica preservada, exatamente uma obrigação ativa no novo fechamento, novo pagamento zerando o total e novo estorno restaurando 300 sem duplicar valores.

## Otimização

O resumo não carrega mais o cadastro inteiro de pessoas. Busca somente IDs presentes nas listas limitadas de contas vencidas/a vencer, mantendo nomes e fallback existentes. O overload anterior permanece disponível aos demais serviços que precisam dele.

A leitura de compras importadas projeta apenas os campos usados na interpretação; não materializa a entidade completa. O filtro obrigatório de workspace introduzido na atividade de isolamento foi preservado. Não se restringiu o histórico arbitrariamente porque recorrências/projeções dependem dele.

## Validação e entrega

- RED financeiro confirmado antes da alteração: dois cenários falharam com 600 em vez de 300; cenário sem consolidação passou.
- GREEN: 42 testes de dashboard/faturas aprovados, zero falhas e zero ignorados. Cobertura Cobertura/OpenCover gerada em TestResults/financial-integrity/2a5b0137-114a-40da-9907-b2f05d26c7a4. Gate global de 80% coordenado pela atividade de isolamento.
- Entrega depende também do gate global de 80% e pipelines DEVELOP/main verdes. Nenhum commit/push realizado nesta atividade.


