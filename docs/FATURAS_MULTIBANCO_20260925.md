# Importação multibanco — 25/09/2026

## Comportamento

O fluxo de conciliação aceita Bradesco mensal com texto, Bradesco aberto em imagens, Nubank, Mercado Pago e BMG nos layouts fornecidos. O limite do arquivo é 128 MiB e 20 páginas. O fallback de texto para fontes/criptografia incompatíveis com PdfPig processa até 16 MiB. Não há IA ou envio a serviços externos.

Nubank: somente tabelas TRANSAÇÕES; o total vem de RESUMO DA FATURA ATUAL, nunca das ofertas de parcelamento. Mercado Pago: somente Detalhes de consumo. BMG: somente Lançamentos até VALOR TOTAL DA FATURA. Pagamentos/saldo anterior participam da conferência, sem criar novas contas. Compras repetidas permanecem independentes e estornos negativos. Importa-se apenas a parcela presente no documento; não são geradas parcelas futuras.

Os parsers de texto conferem compras + saldo anterior + pagamentos com o total impresso. Diferença até R$ 0,05 é explicitamente avisada e mantém os valores das linhas; diferença maior impede leitura parcial. A tolerância cobre arredondamento do documento, não o matching entre contas. OCR Bradesco continua exigindo subtotais exatos. A revisão humana continua obrigatória.

## PDFs protegidos e contratos

POST /api/v1/faturas/{faturaId}/conciliacoes recebe arquivo e senha opcional no multipart. A senha tem no máximo 128 caracteres, passa ao processo local via stdin e não é persistida ou adicionada à linha de comando. Temporários são removidos. Python usa pypdf[crypto] 6.10.0, instalado no Docker/CI em venv com Pillow do sistema. Erros de senha geram mensagem acionável sem criar contas.

ConciliacaoFaturaResponse adiciona avisoLeitura e totalDocumento opcionais. A migração AddInvoiceReadMetadata acrescenta somente duas colunas nullable à sessão. Revisões anteriores permanecem compatíveis. Reabertura pelo hash preserva os avisos e não duplica contas. Swagger e tipos frontend são gerados juntos.

## Evidências

Amostras pessoais verificadas localmente, sem versionamento dos PDFs ou senha:

- Bradesco aberto: 80 itens, R$ 16.358,45; subtotais exatos.
- Nubank: 52 itens, R$ 2.881,31; total impresso R$ 2.881,30; aviso de R$ 0,01, sem ajustar valores.
- Mercado Pago: 3 itens, R$ 2.168,16; total exato.
- BMG: 5 itens, R$ 2.800,49; total exato.

TDD: parsers com ofertas, parcelas, estornos, duplicatas, datas/totais inválidos; upload HTTP transporta senha e mantém aviso/total ao retomar. Fixture criptografada sintética cobre senha ausente/incorreta/correta. CI Linux executa Python e OCR nativos. Validar build, cobertura >=80%, contrato e CI antes de DEV e main.

## Entrega e rollback

Baseline produção: backend 152b23e55daf33b7e3dd3d2f33a68665635a79e2, frontend a10e034290781b55020cd879884b8ec159a433dd. Trabalho em worktrees isoladas. Promover develop, verificar CI e Railway DEV, depois main e Railway/Vercel produção.

Rollback: reverter commits de entrega primeiro no frontend, depois backend, e redeployar os baselines. Manter as colunas nullable (o backend anterior as ignora); não executar Down em produção nem apagar sessões/contas. A versão anterior volta ao limite 5 MB e perde leitura dos novos layouts, mas preserva dados.
