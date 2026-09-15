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
- O extrator padrao deixou de ser apenas simulado para PDFs: arquivos com texto embutido agora passam por leitura deterministica, com normalizacao dedicada para os layouts de fatura Bradesco e Nubank validados localmente.
- A heuristica de importacao passou a reconhecer o texto normalizado de `FATURA_CARTAO` e gerar um item revisavel por transacao, preservando metadata como emissor, portador, cartao final, parcela, moeda e estorno sem efetivacao automatica.
- Confirmar ou rejeitar item atualiza apenas o estado de revisao da importacao nesta fase; nao ha efetivacao automatica de `ContaPagar`, `ContaReceber`, `Movimentacao` ou `CompraCartao`, em linha com a decisao canonica de revisao humana obrigatoria no MVP.
- O reprocessamento substitui integralmente os itens sugeridos da importacao e reexecuta a extracao/sugestao com base no texto bruto e no artefato armazenado, preservando o mesmo registro raiz da importacao.
- Falhas inesperadas no extrator ou na heuristica passaram a ser degradadas para `ERRO_EXTRACAO` com logging explicito, evitando resposta 500 no fluxo de importacao e preparando a observabilidade base exigida nesta fase.
- A fase 9 nao introduziu modulo de negocio novo; o foco foi consolidar o MVP com README, documentacao local e validacao final coerentes com o estado real do repositorio.
- O pipeline e a documentacao de qualidade passaram a refletir explicitamente o caminho de coverage usado no backend e a dependencia dos secrets de Sonar para enforcement remoto do gate.
- Como extensao pos-MVP aprovada localmente, `ContaBancaria` passou a suportar `LimiteCartoesCompartilhado`, com `Cartao` expondo limite efetivo, comprometido e disponivel calculados a partir das compras em cartao ainda abertas.
- O limite compartilhado nao substitui fisicamente o `LimiteCredito` existente no cadastro do cartao; ele prevalece apenas como limite efetivo quando a conta bancaria vinculada estiver configurada para compartilhamento.
- O modulo `ComprasPlanejadas` entrou como CRUD proprio, com entidade dedicada, tabela `compras_planejadas`, vinculo obrigatorio com `ContaGerencial` e `Pessoa` responsavel, sem gerar `ContaPagar` automaticamente neste corte.
- A migration `20260405221837_PostMvpLimiteCompartilhadoEComprasPlanejadas` consolida o novo campo em `contas_bancarias` e a nova tabela do planejador de compras.
- `ContaGerencial` passou a expor `AceitaLancamentos` como informacao derivada de leitura, permitindo diferenciar conta estrutural de conta lancavel sem alterar a modelagem fisica.
- `ContaPagar`, `ContaReceber` e `ComprasPlanejadas` agora validam explicitamente que a conta gerencial usada nao seja conta pai com filhos, bloqueando lancamentos em contas estruturais.
- O dashboard ganhou leitura gerencial por conta com dois endpoints novos, consolidando `RateiosContaGerencial` por `DataEmissao` e mantendo essa visao separada do fluxo de caixa ja existente.
- O drill-down gerencial foi adicionado em `dashboard/contas-gerenciais/lancamentos`, retornando a composicao de contas a pagar e a receber que formam o total da conta no periodo selecionado.
- `CompraPlanejada` passou a persistir `Link` opcional com validacao de URL absoluta, mantendo o payload HTTP e os DTOs resumido/detalhado alinhados ao frontend.
- `ContaPagar` e `CompraPlanejada` passaram a exigir `ContaGerencial` lancavel do tipo `Despesa`, enquanto `ContaReceber` exige `ContaGerencial` lancavel do tipo `Receita`.
- A migration `20260406174804_PostMvpCompraPlanejadaLink` adiciona a coluna `Link` em `compras_planejadas` sem alterar o restante da modelagem financeira.
- As sugestoes de importacao deixaram de duplicar `textoExtraido` dentro do payload de cada item, reduzindo ruido de revisao e armazenamento redundante nos casos de fatura com muitos itens.
- `ContaGerencial` passou a aceitar o papel `EhPadraoRecebimentoFaturaCartao`, com validacao de unicidade e restricao ao tipo `Receita`.
- A migration `20260406200908_PostMvpImportacoesClassificadas` adiciona classificacao persistida por item importado e cria `Recebimento de divida` quando ainda nao existir nenhuma conta padrao para esse fluxo.
- A confirmacao de `CompraCartao` na importacao agora pode gravar `ContaGerencial`, `Responsavel` e gerar uma `ContaReceber` pendente para reembolso de terceiro, usando a conta gerencial padrao de recebimento de fatura.
- `ItemImportadoWhatsapp` passou a manter `ChaveAprendizado`, permitindo prever conta gerencial, responsavel e geracao de conta a receber com base no historico confirmado de itens semelhantes.
- A migration `20260406205018_PostMvpImportacaoAprendizadoRecorrencia` adiciona `DescricaoAjustada` e `MarcarComoRecorrente` em `ItemImportadoWhatsapp`, preparando o aprendizado de nome amigavel e previsao futura por recorrencia.
- A revisao de `CompraCartao` agora pode renomear o lancamento sem alterar o payload bruto extraido; quando houver `ContaReceber` gerada no fluxo, a descricao amigavel passa a ser usada no titulo do recebimento.
- A predicao historica de itens importados foi ampliada para sugerir tambem nome amigavel e recorrencia mensal, alem de conta gerencial, responsavel e geracao de conta a receber.
- `CompraCartao` passou a exigir `ContaGerencial` e `Responsavel` na confirmacao, impedindo aprovacao sem classificacao minima suficiente para o dashboard gerencial.
- A importacao de WhatsApp passou a ter aprovacao explicita no nivel agregado, usando `POST /api/v1/importacoes-whatsapp/{id}/confirmar` para travar a revisao e `POST /api/v1/importacoes-whatsapp/{id}/reabrir` para liberar nova edicao.
- Enquanto a importacao estiver em `PENDENTE_REVISAO`, itens confirmados ou rejeitados podem ser revisados novamente; ao reabrir uma importacao aprovada, os itens retornam a `SUGERIDO` preservando os dados classificados para novo ajuste.
- O fluxo de caixa futuro passou a considerar apenas compras importadas de cartao vindas de importacoes aprovadas, junto com recorrencias pendentes, com protecao contra duplicidade de parcelas ja previstas.
- Itens importados sao encerrados na aprovacao da importacao, sem etapa operacional posterior neste fluxo.
- O dashboard passou a aceitar `MesReferencia` no formato `yyyy-MM`, usando o mes civil completo como janela para resumo, fluxo de caixa, consolidacao gerencial, serie gerencial e drill-down.
- Quando `MesReferencia` e informado, o backend passa a projetar o mes futuro inteiro com base em recorrencias, compras em cartao recorrentes aprovadas e parcelas futuras ainda nao materializadas, sem duplicar a serie quando a parcela real seguinte ja existe.
- A central de previsao foi introduzida no backend como leitura unificada, com endpoints de resumo e itens por dia, origem e status, sem criar entidade persistida separada so para previsoes.
- A central de previsao marca explicitamente cada item como `Realizado`, `Previsto` ou `Substituido`, substituindo previsoes por ocorrencias reais equivalentes para evitar dupla contagem no dashboard.
- `CompraPlanejada` passou a poder ser convertida em `ContaPagar` pelo fluxo existente de criacao de conta, preservando vinculo reverso entre os registros e impedindo reconversao da mesma compra.
- A migration `20260408040021_PostMvpCentralPrevisaoECompraPlanejadaContaPagar` adiciona o vinculo de origem em `contas_pagar` e o rastreio de conversao em `compras_planejadas`.
- A central de previsao do dashboard deixou de incluir `CompraPlanejada` enquanto o registro estiver apenas no planejador; o vinculo de origem em `ContaPagar` ficou apenas para rastreabilidade, sem virar origem propria da previsao.
- `GET /api/v1/recorrencias` passou a retornar tambem a conta de origem da regra, com `descricao`, `valorLiquido`, `pessoaNome` e `responsavelNome`, permitindo uma tela de recorrencias mais operacional sem nova entidade persistida.
- A central de previsao passou a considerar `Realizado` apenas quando a conta materializada ja estiver liquidada/recebida; recorrencias, parcelas e contas futuras ainda pendentes agora aparecem como `Substituido`, eliminando a inflacao indevida do realizado no dashboard.
- No dashboard por `MesReferencia`, recorrencias passaram a entrar apenas em meses futuros; no mes atual ou em meses passados, o fluxo ignora projecoes de recorrencia e a central de previsao oculta itens de `Recorrencia` e `ContaFuturaGerada`.

- As listagens operacionais de contas, movimentacoes, compras planejadas e recorrencias passaram a retornar `summary` filtrado no mesmo payload, permitindo totalizadores corretos antes da paginacao.
- `Pessoa` passou a aceitar varias `ChavesPix`, com tipo e valor normalizado, permitindo manter mais de uma chave Pix por cadastro sem abrir modulo separado.
- O contrato de pessoas foi ampliado com `ChavesPix` em create, update e detail, e a persistencia ganhou a tabela `pessoas_chaves_pix` com unicidade por pessoa, tipo e chave.
- A migration `20260409043419_AddPessoaChavesPix` adiciona o suporte estrutural de multiplas chaves Pix por pessoa.
- O parser deterministico do PDF Bradesco foi corrigido para nao tratar marcadores de parcela (`2/12`, `5/7`, `10/12`) como se fossem valor monetario quando vierem em token separado da descricao.
- A normalizacao de itens de fatura do Bradesco passou a preservar a parcela no proprio texto do item revisavel e o gerador de sugestoes passou a reconhecer corretamente valores acima de mil reais sem truncar os digitos iniciais.
- A importacao `extratoCartao (19).pdf` foi reprocessada apos a correcao e passou a bater integralmente com a validacao manual da planilha do Bradesco, sem faltas nem sobras de lancamentos.
- O parser da Nubank passou a rejeitar datas e cabecalhos como se fossem valor monetario, impedindo que blocos como `EMISSAO E ENVIO 06 ABR 2026` virem compras falsas na revisao.
- A importacao `Nubank_2026-04-13.pdf` foi reprocessada apos o ajuste e deixou de gerar o falso lancamento `EMISSAO E ENVIO`.
- `ContaGerencial` passou a aceitar `ResponsavelPadraoId` opcional, com validacao de existencia no cadastro de pessoas e retorno do nome no resumo/detalhe para apoiar fluxos operacionais.
- `CompraPlanejada` passou a expor a acao `Realizar`, que converte a intencao em fluxo financeiro efetivo conforme a forma de pagamento escolhida.
- Quando a forma de pagamento faz baixa automatica e nao e cartao, a realizacao gera `ContaPagar` ja liquidada e `MovimentacaoFinanceira` na data da compra.
- Quando a forma de pagamento nao faz baixa automatica, a realizacao gera `ContaPagar` pendente com vencimento informado pelo operador.
- Quando a forma de pagamento e cartao, a realizacao gera parcelas de `ContaPagar` por competencia de fatura, usando fechamento e vencimento do cartao para distribuir as parcelas pelos meses corretos.
- O parcelamento de cartao deixou de reutilizar a mesma `DataEmissao` em todas as parcelas; agora cada parcela usa a competencia propria para evitar concentracao indevida na mesma fatura.
- A revisao de importacao passou a calcular predicao de classificacao em duas camadas: historico confirmado continua com prioridade maxima, e compras de cartao sem historico agora recebem sugestao heuristica para categorias obvias como supermercado, farmacia/drogaria, transporte por app e lanches/delivery.
- A heuristica de classificacao reaproveita o `ResponsavelPadraoId` da `ContaGerencial`, permitindo pre-preencher responsavel junto com a conta sugerida sem acoplar IA generativa ao fluxo transacional.
- O Swagger/OpenAPI passou a documentar explicitamente os dois modos de autenticacao suportados pelo backend: `Bearer` para `JwtBearer/Auth0` e `DebugUser` para o header local `X-Debug-User`.
- Operacoes protegidas por `[Authorize]` agora aparecem no documento OpenAPI com requisito de seguranca explicito, deixando a documentacao mais usavel em homologacao local e no futuro ciclo de Auth0.
- O bootstrap de `Application` deixou de depender de uma lista manual de `AppService`; os servicos publicos terminados em `AppService` agora sao registrados automaticamente por convencao, reduzindo risco de esquecer wiring ao abrir novos modulos.
- A validacao desse bootstrap passou a ter teste dedicado em `ControleFinanceiro.Application.Tests`, garantindo que todos os `AppService` publicos continuem registrados como `Scoped` e que o contrato explicito de `IBootstrapCatalogService` siga preservado.
- Imports de cartao ja aprovados agora podem concluir a materializacao financeira depois, por `completar-fechamento-fatura`, sem precisar reabrir a importacao.
- Esse fechamento tardio sincroniza `Faturas`, cria ou atualiza a `ContaPagar` visivel da fatura com o valor total e agrega o rateio a partir dos itens confirmados da revisao.
- O fluxo permanece idempotente: se a importacao ja tiver sido materializada financeiramente, a nova chamada nao duplica contas, faturas nem parcelas futuras.
- Compras realizadas em cartao deixaram de gerar `MovimentacaoFinanceira` economica visivel; elas passam a existir apenas em `Faturas` e nos itens internos de cartao, e a tela de `Movimentacoes` passou a ignorar registros `CANCELADA` por padrao.
- Itens de estorno aprovados na revisao da fatura passaram a ser materializados como compras internas de cartao com valor negativo, abatendo o total e o rateio agregado da fatura em vez de bloquear o fechamento.
- `ContaPagar` operacional continua positiva, mas a composicao interna da fatura agora aceita saldo negativo por item para representar creditos reais de cartao, como estorno e cancelamento parcial.
- O fechamento de importacao de cartao passou a recalcular a competencia atual da fatura priorizando `DataVencimento`, impedindo que parcelas antigas da importacao do Bradesco abram `Faturas` em meses passados quando o fechamento correto e o mes atual da competencia.
- A materializacao de compras de cartao recorrentes foi corrigida no fluxo ativo de fechamento para gerar somente a fatura atual e as competencias futuras, sem espalhar previsoes para meses anteriores.
- `GET /api/v1/faturas` ganhou filtros adicionais por `DataFechamentoInicial` e `DataFechamentoFinal`, alem de ordenacao segura por `CartaoNome`, permitindo a tela de faturas filtrar e ordenar por cartao sem erro.
- O `summary` da listagem de faturas passou a retornar agregacao por cartao e por competencia do mes, viabilizando totalizadores operacionais no frontend.
- O contrato de `Pessoas` voltou a retornar `CpfCnpj` normalizado completo em detalhe e listagem, deixando a mascara visual sob responsabilidade do frontend e evitando truncamento do documento na grade.
- A autenticacao de desenvolvimento passou a rejeitar multiplos headers `X-Debug-User`, emitir claims consistentes de usuario e permitir testes explicitos com cliente anonimo ou autenticado.
- O rate limiter foi reposicionado apos a autenticacao para particionar corretamente por usuario autenticado antes de cair no fallback por IP.
- A confirmacao de importacao de fatura passou a exigir `ContaGerencialPadraoId`, validar cartao, forma de pagamento, recebedor e conta gerencial no tenant corrente, e persistir o rateio dos itens importados.
- O audit trail passou a sanitizar campos sensiveis de pessoas, chaves Pix, contas bancarias, cartoes e tokens antes de gravar `BeforeJson`/`AfterJson`.
- Os pacotes OpenTelemetry foram atualizados para versoes sem vulnerabilidades conhecidas nas fontes NuGet atuais.

## Pendencias nao criticas
- configurar secrets reais de SonarQube/SonarCloud no CI para ativar o quality gate remoto.
- ampliar a cobertura combinada das camadas com testes adicionais focados em Application e Infrastructure conforme os modulos financeiros avancarem.
- A fase 1 do modelo de workspaces foi iniciada sem quebra de compatibilidade: o tenant continua tecnicamente como Familia, mas o backend agora lista participacoes, permite trocar o workspace ativo e aplica limite global de 3 participacoes por usuario.


## 11/09/2026 — Correção de itens cancelados na fatura

- `RemoverDaFaturaAsync` aceita contas de cartão `EM_FATURA` ou `CANCELADA`. A segunda representa o crédito sintético de estorno exibido na fatura.
- A remoção reutiliza as regras existentes: exclui a conta e parcelas futuras do grupo, além dos reembolsos associados ainda não recebidos; rejeita faturas fechadas/pagas e reembolsos recebidos.
- Não altera cálculos de cancelamento, entidades, migrations, DTOs, rotas ou payloads. O cancelamento comum continua gerando a representação de estorno; a nova ação explícita permite corrigir um crédito indevido.
- Testes de integração cobrem remoção de estorno em fatura aberta, bloqueio em fatura fechada e transferência de compra para outro cartão com a mesma data de vencimento. A troca de cartão já funciona pelo endpoint de edição existente.
- TDD: o cenário de remoção retornou 400 antes da alteração e 204 depois; a suíte de fluxo de contas a pagar permaneceu verde.
- Validação final: 792 testes aprovados e 3 ignorados por dependência de PostgreSQL no ambiente SQLite; cobertura consolidada de linhas 80,6% (gate 80%). Build Release sem erros/avisos. Auditoria sem vulnerabilidades nos projetos de produção; alerta preexistente High de SQLitePCLRaw.lib.e_sqlite3 2.1.10 nos projetos de testes (GHSA-2m69-gcr7-jv3q), registrado sem ampliar o escopo para atualização de dependências. Não publicado.



## 2026-09-12 — Ciclo de recorrência revisado

- Cartão permanece EM_FATURA em todos os meses; outras formas usam FUTURO e passam a PENDENTE na virada. Geração mensal de cartão deduplica por regra e mês do vencimento e completa a janela de seis meses, limitada pela data fim.
- Pausa cancela FUTURO; retomada restaura somente as canceladas pela pausa do mês atual em diante, preservando IDs. Pausas longas não geram retroativos. Encerramento manual exige pausa; data fim encerra automaticamente.
- Migration aditiva e reversível, contrato OpenAPI atualizado e modelo sem mudanças pendentes. SQL PostgreSQL gerado e revisado sem execução em banco real.
- Validação: 803 testes aprovados, 3 ignorados; cobertura 80,7%; build Release aprovado. Auditoria da API sem vulnerabilidades. Detalhes e reversão em RECORRENCIA_CICLO_20260912.md. Não publicado; duplicatas históricas não foram removidas.

## 2026-09-13 — Importação de PDF Bradesco

- Leitura determinística do PDF mensal com texto embutido, sem IA, e suporte ao layout anterior do aplicativo.
- Vencimento da fatura, número/total de parcelas e estornos preservados na confirmação. Só a parcela do documento é criada.
- Pagamentos e saldo anterior entram apenas na conferência; divergência no total bloqueia a prévia do layout mensal.
- Compras iguais recebem chaves independentes e estáveis na reimportação.
- Amostra real conferida integralmente; arquivo pessoal não versionado.
- Testes sintéticos cobrem linhas partidas, sinal negativo separado, câmbio, compras iguais e total divergente; API cobre confirmação/reimportação e Swagger; frontend cobre envio dos metadados sem agente.
- Validação .NET 10: 843 testes aprovados, 3 testes PostgreSQL ignorados no fallback local SQLite; cobertura consolidada de linhas 81,4%; build Release sem erros/avisos; contratos OpenAPI sincronizados. CI PostgreSQL exigida antes da promoção.
- Promoção: primeiro develop, depois main somente com CI verde.


## 2026-09-14 — Conciliação de fatura (em implementação, não publicada)

Base: PROD-12 do Claude, commit 4827d50. Worktree isolado em `.local-runtime/conciliacao-backend`, branch `codex/conciliacao-fatura-20260914`.

- A conciliação bancária OFX/CSV continua vinculada a movimentações. A extensão PDF reutiliza o agregado de sessão/itens e vincula contas a pagar da fatura.
- Implementados: sessão PDF com hash e chaves por ocorrência, reabertura idempotente, sugestões por descrição/data/parcela/sinal e tolerância proposta de R$0,05, proteção de ambiguidade, vínculo único por conta na sessão.
- Vínculo reaproveita a conta e seus metadados. Diferença exige aceite explícito; guarda valor anterior e ajusta somente a parcela, com rateios exatos. Conta liquidada/cancelada e fatura fechada não aceitam ajuste. Repetição não sobrescreve auditoria.
- Migrations aditivas: AddConciliacaoFatura e metadados de concorrência. Ainda não aplicadas em DEV/produção.
- TDD: 295 testes de domínio e 103 de aplicação passaram com cobertura gerada. Dois testes HTTP de conciliação passaram (PDF/reabertura e ajuste/repetição). Suíte geral API/cobertura em verificação; não constitui gate concluído.
- Próximos: criação de itens ausentes, rascunhos tipados, memória por família/campo, reembolso em lote usando o serviço existente, recorrência existente, tratamento de reimportação entre PDFs diferentes, testes de isolamento/concorrência e atualização OpenAPI.
- Pendência identificada na base: contrato do frontend bancário não corresponde aos DTOs retornados (lista/paginação, datas e sugestão); alinhar antes da integração visual final.
- Sem commits/push/promoção nesta fase. Publicação exige gates completos em DEV e main e verificação Railway. O branch-base inclui outras entregas do Claude; verificar promoção dessas mudanças antes de abrir PR.


## 2026-09-15 — Memória, criação e reembolsos da conciliação (em andamento)

- Itens ausentes criam somente a parcela do PDF. Conta, vínculo, reembolso e memória usam transação serializável; falhas revertem o conjunto.
- Recorrência reutiliza o serviço existente e respeita data fim, sem duplicar o mês atual. Validação de parcela importada considera a fatura selecionada, mesmo quando a compra original pertence a mês fechado.
- Memória por família/cartão/estabelecimento e campo confirmado, com opção de não aprender. Rascunhos persistem sem criar contas e verificam versão para impedir sobrescrita concorrente.
- Prévia de reembolso usa ParcelamentoHelper, informa pagadores, parcelas, valores e vencimentos sem gravações. Swagger exportado e tipos frontend regenerados.
- Corrigido defeito reproduzido no serviço compartilhado de reembolso: múltiplos pagadores e categorias dividiam o rateio pelo subtotal de um pagador, gerando valor negativo/erro 500. Base proporcional corrigida para o total original; teste HTTP verifica 60/40 por conta.
- Testes dirigidos: 12 casos de reembolso/conciliação passaram após a correção; 8 casos de prévia/reembolso passaram; teste de Swagger passou. Suíte completa e cobertura de 15/09 em execução, ainda sem gate final.
- Permanecem pendentes: revisão de segurança/concorrência/reimportação entre PDFs, compatibilidade do frontend bancário, validação visual e gates DEV/main/Railway. Sem publicação.

- Checkpoint 15/09: backend 884 testes completos + 2 testes HTTP OFX/CSV aprovados, 3 ignorados no SQLite; cobertura consolidada 80,5% de linhas. Frontend 1.373 testes completos aprovados, 87,69% linhas e 80,13% branches; mais 2 testes do adaptador bancário aprovados. Tipos e lint sem erros.
- Incompatibilidade bancária corrigida no frontend: lista real, datas, status EmRevisao, sugestão plana e contagens por item; limite de 50 sessões explicitado.
- Remotos atualizados: frontend develop avançou 30 commits; necessária integração da base e repetição dos gates antes de publicar. Checkpoint local não representa entrega final.

- Validação integrada concluída: backend 886 aprovados, 3 ignorados, cobertura 80,5%, Release e modelo EF aprovados. Frontend 1.375 aprovados, linhas 87,64%, branches 80,12%, lint/build aprovados. Procedimento de promoção e reversão em CONCILIACAO_FATURA_20260915.md. Publicação ainda pendente.
- Revisão do SQL em DEV identificou mapeamento de memória não registrado no contexto. Corrigido por ApplyMemoriaEstabelecimentoConstraints, preservando dados e acrescentando unicidade, FK e concorrência; migration anterior não foi reescrita. Teste de modelo reproduziu a falha antes da correção. Após ajuste: 3 testes de modelo e 6 testes HTTP de conciliação aprovados. Promoção de main aguarda CI/DEV desta correção.
