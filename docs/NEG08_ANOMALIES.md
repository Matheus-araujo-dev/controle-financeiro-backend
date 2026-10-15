# NEG-08 — Alertas de cobranças

Escopo do documento 39: regras determinísticas no backend, sem IA, sem escrita automática e sem migration. O NEG-08 do documento 38 (reembolsos em massa) é distinto.

GET /api/v1/dashboard/anomalias?mesReferencia=yyyy-MM retorna mês, início do histórico, completo, contasAnalisadas e itens com regra, valor atual, mediana e evidências (IDs das contas, datas, valores). Exige autenticação e respeita o workspace. Mês obrigatório entre 0002-01 e 9998-12. O dashboard usa types gerados do OpenAPI, serviço HTTP e links às contas; não há formulário de edição novo.

Recorte por DataEmissao: mês selecionado e três anteriores, todas as contas/cartões. Só contas positivas, não canceladas, não parceladas; itens de cartão são elegíveis, obrigação consolidada não. Projeções e importações não materializadas não entram. Até 5.000 contas elegíveis; ao detectar a 5.001ª, completo=false e itens vazios, sem declarar ausência de problemas. contasAnalisadas é o número lido, não um total global nesse caso.

Regras conservadoras documentadas como decisão local:
- Duplicidade provável: descrição normalizada por espaços/caixa, mesmo recebedor, responsável, conta, cartão, data e valor (inclusive regras de recorrência distintas). Mostra todos os IDs envolvidos; não confirma duplicidade.
- Comparações exigem exatamente uma cobrança em cada um dos quatro meses e mesmo contexto, sem misturar regras de recorrência distintas. Mediana dos três anteriores como referência. Sem histórico ou com mês ambíguo, não comparar.
- Aumento recorrente: mesma regra de recorrência, ao menos 20% e R$ 10 acima da mediana.
- Valor incomum não recorrente: ao menos 50% e R$ 50 acima da mediana.
- Revisar recorrência: quatro meses consecutivos da mesma recorrência; convite para confirmar necessidade. Não há dado de utilização, então não se afirma assinatura esquecida/sem uso.

Interface identifica período/base, dados insuficientes, falha e carregamento. Sinais exigem revisão humana; resultado vazio não garante ausência de cobranças indevidas. Trocar mês não reaproveita diagnóstico anterior.

TDD red confirmado: endpoint 404; detector vazio falhou em duplicidade, aumento e valor incomum. Implementação inicial: oito testes de regras e dois de API verdes; seis testes de interface verdes. Ampliação e gates completos em andamento.

Rollback: baseline backend 130823b9c79536becc3c0a2a9b8e3c09f2ac596f; frontend b3894af375e84d8e371cf5ec660fb8925408de44. Reverter os commits da entrega, passando develop/main; sem migração ou alteração de dados. Publicar backend antes de frontend; na reversão, retirar o consumidor frontend antes do endpoint backend.

## Validação local final (28/09/2026)
- TDD adicional reproduziu e corrigiu duplicidade entre regras de recorrência distintas, mantendo a comparação histórica isolada. Dez testes do detector e dez testes de API/ciclo de fatura aprovados, incluindo autenticação, workspace, créditos, cancelamentos, parcelas e limite incompleto.
- Backend: 931 aprovados e seis skips dependentes de provider/runtime; gate total de linhas 80,7%; nenhum pacote vulnerável. CI deve validar PostgreSQL e OCR.
- Frontend: 1.432 testes aprovados; cobertura statements 85,61%, branches 80,42%, functions 82,07%, lines 87,79%. Contratos, lint (zero erros, 45 avisos existentes), tipos, build, auditoria e PWA aprovados; 12 E2E desktop/mobile aprovados.
- Build de produção validado visualmente em desktop/mobile/standalone emulado com APIs simuladas; sem overflow e links disponíveis. Os E2E iniciais revelaram fixtures incompletas de resumo/fluxo, corrigidas antes de passar. Não representa operação financeira autenticada em produção nem instalação PWA real.
- Gates remotos e publicação ainda pendentes. Publicar backend antes do frontend.
