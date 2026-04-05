# Progress Log - Backend

## Ultima fase concluida
- Fase 5: recorrencia concluida com regra persistida, geracao de ocorrencias, pausa, encerramento, edicao pontual e alteracao futura.

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

## Pendencias nao criticas
- configurar secrets reais de SonarQube/SonarCloud no CI para ativar o quality gate remoto.
- ampliar a cobertura combinada das camadas com testes adicionais focados em Application e Infrastructure conforme os modulos financeiros avancarem.
