# Bradesco: extrato aberto em imagem — 25/09/2026

O PDF exportado pelo aplicativo pode conter uma imagem longa sem compressão, repetida e recortada em páginas. O fluxo de conciliação aceita até 128 MiB (requisição multipart com 1 MiB adicional), 20 páginas e imagens de até 40 milhões de pixels. O arquivo de 111.901.008 bytes enviado pelo usuário foi reconhecido localmente: 80 compras/estornos, total R$ 16.358,45, igual à soma dos subtotais R$ 12.790,64 + R$ 3.567,81. A amostra pessoal não é versionada.

## Leitura e revisão

- Texto embutido continua no parser anterior. Imagens usam `IInvoiceImageOcr`, implementado com Tesseract local e Pillow, sem IA ou serviço externo.
- Imagens longas repetidas são reconhecidas uma vez; faixas sobrepostas protegem linhas nas quebras. Compras iguais em posições distintas permanecem independentes.
- O extrato aberto usa o vencimento da fatura selecionada. O ano das compras é calculado pela data de emissão do documento, incluindo virada de ano. Não são inventados números de parcelas que o extrato não informa.
- Pagamentos e saldo anterior entram na conferência do subtotal, mas não viram contas. Estornos continuam negativos.
- Subtotal divergente, data inválida, linha de compra ilegível ou documento incompleto bloqueiam a importação inteira. A revisão humana continua obrigatória: OCR pode errar descrições e datas mesmo com totais corretos.
- Separadores de centavos e de datas omitidos pelo OCR só são normalizados em formatos numéricos restritos, com conferência de todos os subtotais.
- A conciliação gera uma sessão de revisão, nunca contas automaticamente. Hash do arquivo preserva reabertura idempotente.

## Operação

Uploads e imagens ficam em diretórios temporários, removidos ao concluir/falhar. Uma leitura de imagens por processo evita concorrência pesada de OCR. Cada processo tem cancelamento e limite de três minutos; o cliente aguarda até quatro minutos. A imagem extraída é comprimida como PNG para o processamento. A expansão multibanco adiciona metadados opcionais à sessão; veja `FATURAS_MULTIBANCO_20260925.md`.

Docker e CI instalam `python3`, `python3-pil`, `tesseract-ocr` e `tesseract-ocr-por`. `InvoiceOcr:PythonExecutable` e `InvoiceOcr:TesseractExecutable` permitem caminhos locais específicos; em Linux os defaults são usados. Nubank, Mercado Pago e BMG possuem parsers de texto próprios, descritos em `FATURAS_MULTIBANCO_20260925.md`.

## Validação e publicação

TDD no parser e no upload frontend. Testes cobrem subtotais, estornos, compras iguais, imagem longa, virada de ano, erros, upload HTTP acima de 6 MiB e reimportação sem criar contas. O teste nativo de OCR roda no CI Linux com fixture sintética. Qualidade completa local e CI são gates antes de DEV e main.

Rollback: reverter os PRs de frontend/backend na ordem compatível, preservando sessões e lançamentos existentes. A migration aditiva de metadados pode permanecer no rollback; não excluir sessões ou lançamentos. Reverter o backend remove o suporte a imagens/arquivos grandes; o frontend anterior deve ser restaurado antes para voltar a informar o limite de 5 MB.
