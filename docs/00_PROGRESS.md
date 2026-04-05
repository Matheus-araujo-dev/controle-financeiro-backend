# Progress Log - Backend

## Ultima fase concluida
- Fase 9: fechamento do MVP concluido com README atualizado, documentacao local minima, validacao final e consolidacao dos artefatos de quality gate.

## Decisoes locais
- .NET 9 foi adotado porque ja esta disponivel no ambiente e a documentacao permite .NET 9 ou LTS vigente.
- A auditoria inicial foi materializada com `AuditTrailEntry` e stamping base em `AuditableEntity`, sem adiantar regras financeiras.
- As fases 0 e 1 foram revalidadas neste ciclo com build, testes, coverage e ajustes na fixture de integracao para SQLite em memoria.
- A fundacao tecnica foi expandida para incluir `IAppDbContext`, auditoria com usuario/data injetados e quality gate remoto aguardando resultado do Sonar quando configurado.
- O build local precisa usar `-m:1` para evitar falhas silenciosas de paralelismo no ambiente atual.
- O endpoint protegido inicial usa o modo `Development` com header `X-Debug-User`, suficiente para preparar a autenticacao sem acoplar regra de negocio.
- A fase 2 foi implementada mantendo o escopo canonico: CRUDs de apoio, filtros, paginacao, DTOs, controllers, mapeamentos EF e migration versionada.
- A fase 3 introduziu o nucleo financeiro sem antecipar as fases seguintes: parcelamento, rateio obrigatorio, liquidacao, cancelamento e geracao de movimentacao ficaram encapsulados no modulo `Financeiro`.
- Os `Rateios` de `ContaPagar` e `ContaReceber` permaneceram como colecao de dominio, com persistencia explicita em `RateioContaGerencial`, para evitar acoplamento prematuro da modelagem EF.
- O SQLite em memoria continuou sendo usado nos testes de integracao, com ajustes de ordenacao e logging para manter o comportamento deterministico e compativel com o provider.
- A fase 4 passou a tratar compra em cartao como despesa economica na data da compra, com movimentacao economica dedicada e sem saida bancaria real naquele momento.
- `FaturaCartao` foi introduzida como agregacao persistida por `CartaoId + Competencia`, com sincronizacao pragmatica a partir das compras em cartao e pagamento gerando uma unica movimentacao real de saida.
- O calculo de competencia ficou centralizado em `FaturaCartaoCompetencia`, com testes automatizados cobrindo fechamento/vencimento inclusive quando o dia de vencimento cai no mes seguinte.
- A fase 5 materializou `RegraRecorrencia` como entidade persistida e vinculada a `ContaPagar` e `ContaReceber`, sem criar um modulo de rotas separado do nucleo financeiro.
- A geracao de ocorrencias recorrentes ficou explicita por acao de negocio (`gerar-ocorrencias`) e produz previsoes pendentes, sem antecipar liquidacao automatica nem movimentacao financeira real nas novas ocorrencias.
- `Recorrencia` e `parcelamento` ficaram mutuamente exclusivos neste corte inicial para evitar mistura de duas regras de geracao distintas antes da fase de dashboard e fluxo de caixa.
- A alteracao futura parte da ocorrencia selecionada e propaga o novo template apenas para ocorrencias posteriores ainda editaveis, preservando o historico ja realizado.
- A fase 6 foi implementada como modulo de leitura (`DashboardAppService` + `DashboardController`), sem introduzir novas entidades ou migrations fora do escopo canonico.
- O resumo executivo passou a aceitar `DataReferencia` e `DiasProjetados` para manter o calculo testavel e deterministico, sem alterar os endpoints canonicos definidos para a fase.
- Na visao de caixa, compras em cartao abertas entram na projecao pela data prevista da fatura; na visao economica, entram pela data da compra via movimentacao economica, evitando dupla contagem do pagamento da fatura.
- Contas abertas vencidas antes da janela de projecao sao concentradas no primeiro dia do fluxo para expor risco imediato sem adulterar o saldo base bancario.
- A fase 7 foi implementada com o agregado `ImportacaoWhatsapp` e a entidade filha `ItemImportadoWhatsapp`, mantendo status de importacao e status de item separados para suportar reprocessamento e revisao humana.
- O pipeline da importacao foi mantido sincrono neste MVP inicial, mas desacoplado por `IFileStorage`, `IDocumentExtractor` e `IImportSuggestionService`, preparando a futura troca por fila/OCR real sem reescrever a API.
- O armazenamento do artefato usa caminho local controlado em `App_Data/importacoes-whatsapp`, com validacao de `mime type` permitido e sem execucao de qualquer arquivo recebido.
- A extracao e a geracao de sugestoes ficaram simuladas por heuristica local neste corte, suficientes para o fluxo ponta a ponta exigido pela fase sem inventar integracao real de OCR/IA fora da documentacao.
- Confirmar ou rejeitar item atualiza apenas o estado de revisao da importacao nesta fase; nao ha efetivacao automatica de `ContaPagar`, `ContaReceber`, `Movimentacao` ou `CompraCartao`, em linha com a decisao canonica de revisao humana obrigatoria no MVP.
- O reprocessamento substitui integralmente os itens sugeridos da importacao e reexecuta a extracao/sugestao com base no texto bruto e no artefato armazenado, preservando o mesmo registro raiz da importacao.
- A fase 8 reutilizou `ItemImportadoWhatsapp` do tipo `ItemExtrato` como origem do extrato importado, sem criar uma entidade nova de conciliacao fora do modelo canonico mais recente.
- O vinculo manual passou a ser persistido por `MovimentacaoFinanceiraId` opcional em `ItemImportadoWhatsapp`, enquanto `MovimentacaoFinanceira` ganhou operacao explicita de conciliacao e aproveitou `DataConciliacao`/`StatusMovimentacao` ja previstos no dominio.
- As sugestoes de conciliacao sao heuristicas e assistidas, baseadas em valor, data, observacao e tipo de movimentacao sugerido; a confirmacao continua sendo exclusivamente manual nesta fase.
- A conciliacao manual exige item de extrato confirmado, movimentacao bancaria realizada e nao conciliada, gravando auditoria por atualizacao tanto no item importado quanto na movimentacao financeira.
- Falhas inesperadas no extrator ou na heuristica passaram a ser degradadas para `ERRO_EXTRACAO` com logging explicito, evitando resposta 500 no fluxo de importacao e preparando a observabilidade base exigida nesta fase.
- A fase 9 nao introduziu modulo de negocio novo; o foco foi consolidar o MVP com README, documentacao local e validacao final coerentes com o estado real do repositorio.
- O pipeline e a documentacao de qualidade passaram a refletir explicitamente o caminho de coverage usado no backend e a dependencia dos secrets de Sonar para enforcement remoto do gate.

## Pendencias nao criticas
- configurar secrets reais de SonarQube/SonarCloud no CI para ativar o quality gate remoto.
- ampliar a cobertura combinada das camadas com testes adicionais focados em Application e Infrastructure conforme os modulos financeiros avancarem.
