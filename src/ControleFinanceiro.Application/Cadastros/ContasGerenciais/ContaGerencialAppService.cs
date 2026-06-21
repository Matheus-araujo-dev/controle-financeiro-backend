using ControleFinanceiro.Application.Common.Pagination;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.Common.Validation;
using ControleFinanceiro.Contracts.Cadastros.ContasGerenciais;
using ControleFinanceiro.Contracts.Common;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Cadastros.ContasGerenciais;

public sealed class ContaGerencialAppService(IAppDbContext dbContext)
{
    public async Task<PagedResult<ContaGerencialResumoResponse>> ListarAsync(
        ContaGerencialListQueryRequest query,
        CancellationToken cancellationToken)
    {
        var consulta = dbContext.ContasGerenciais.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var termo = $"%{query.Search.Trim()}%";
            consulta = consulta.Where(x =>
                EF.Functions.Like(x.Descricao, termo) ||
                (x.Codigo != null && EF.Functions.Like(x.Codigo, termo)));
        }

        if (query.Tipo.HasValue)
        {
            consulta = consulta.Where(x => x.Tipo == MapearTipo(query.Tipo.Value));
        }

        if (query.Tipos is { Count: > 0 })
        {
            var tipos = query.Tipos.Select(MapearTipo).ToArray();
            consulta = consulta.Where(x => tipos.Contains(x.Tipo));
        }

        if (query.ContaPaiId.HasValue)
        {
            consulta = consulta.Where(x => x.ContaPaiId == query.ContaPaiId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.ContaPai))
        {
            var contaPai = $"%{query.ContaPai.Trim()}%";
            consulta = consulta.Where(x =>
                x.ContaPaiId.HasValue &&
                dbContext.ContasGerenciais.Any(parent =>
                    parent.Id == x.ContaPaiId.Value &&
                    EF.Functions.Like(parent.Descricao, contaPai)));
        }

        if (query.ResponsavelPadraoId.HasValue)
        {
            consulta = consulta.Where(x => x.ResponsavelPadraoId == query.ResponsavelPadraoId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.ResponsavelPadrao))
        {
            var responsavelPadrao = $"%{query.ResponsavelPadrao.Trim()}%";
            consulta = consulta.Where(x =>
                x.ResponsavelPadraoId.HasValue &&
                dbContext.Pessoas.Any(pessoa =>
                    pessoa.Id == x.ResponsavelPadraoId.Value &&
                    EF.Functions.Like(pessoa.Nome, responsavelPadrao)));
        }

        if (query.Ativo.HasValue)
        {
            consulta = consulta.Where(x => x.Ativo == query.Ativo.Value);
        }

        if (query.AceitaLancamentos.HasValue)
        {
            consulta = query.AceitaLancamentos.Value
                ? consulta.Where(x => !dbContext.ContasGerenciais.Any(child => child.ContaPaiId == x.Id))
                : consulta.Where(x => dbContext.ContasGerenciais.Any(child => child.ContaPaiId == x.Id));
        }

        if (query.EhPadraoRecebimentoFaturaCartao.HasValue)
        {
            consulta = consulta.Where(x => x.EhPadraoRecebimentoFaturaCartao == query.EhPadraoRecebimentoFaturaCartao.Value);
        }

        consulta = (query.SortBy ?? string.Empty).ToLowerInvariant() switch
        {
            "codigo" => query.SortDirection == SortDirection.Desc
                ? consulta
                    .OrderByDescending(x => x.Codigo == null)
                    .ThenByDescending(x => x.Codigo)
                    .ThenByDescending(x => x.Descricao)
                : consulta
                    .OrderBy(x => x.Codigo == null)
                    .ThenBy(x => x.Codigo)
                    .ThenBy(x => x.Descricao),
            "tipo" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.Tipo).ThenByDescending(x => x.Descricao)
                : consulta.OrderBy(x => x.Tipo).ThenBy(x => x.Descricao),
            "contapaidescricao" => query.SortDirection == SortDirection.Desc
                ? consulta
                    .OrderByDescending(x => dbContext.ContasGerenciais
                        .Where(parent => parent.Id == x.ContaPaiId)
                        .Select(parent => parent.Descricao)
                        .FirstOrDefault())
                    .ThenByDescending(x => x.Descricao)
                : consulta
                    .OrderBy(x => dbContext.ContasGerenciais
                        .Where(parent => parent.Id == x.ContaPaiId)
                        .Select(parent => parent.Descricao)
                        .FirstOrDefault())
                    .ThenBy(x => x.Descricao),
            "responsavelpadraonome" => query.SortDirection == SortDirection.Desc
                ? consulta
                    .OrderByDescending(x => dbContext.Pessoas
                        .Where(pessoa => pessoa.Id == x.ResponsavelPadraoId)
                        .Select(pessoa => pessoa.Nome)
                        .FirstOrDefault())
                    .ThenByDescending(x => x.Descricao)
                : consulta
                    .OrderBy(x => dbContext.Pessoas
                        .Where(pessoa => pessoa.Id == x.ResponsavelPadraoId)
                        .Select(pessoa => pessoa.Nome)
                        .FirstOrDefault())
                    .ThenBy(x => x.Descricao),
            "aceitalancamentos" => query.SortDirection == SortDirection.Desc
                ? consulta
                    .OrderByDescending(x => !dbContext.ContasGerenciais.Any(child => child.ContaPaiId == x.Id))
                    .ThenByDescending(x => x.Descricao)
                : consulta
                    .OrderBy(x => !dbContext.ContasGerenciais.Any(child => child.ContaPaiId == x.Id))
                    .ThenBy(x => x.Descricao),
            "ehpadraorecebimentofaturacartao" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.EhPadraoRecebimentoFaturaCartao).ThenByDescending(x => x.Descricao)
                : consulta.OrderBy(x => x.EhPadraoRecebimentoFaturaCartao).ThenBy(x => x.Descricao),
            "ativo" => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.Ativo).ThenByDescending(x => x.Descricao)
                : consulta.OrderBy(x => x.Ativo).ThenBy(x => x.Descricao),
            _ => query.SortDirection == SortDirection.Desc
                ? consulta.OrderByDescending(x => x.Descricao)
                : consulta.OrderBy(x => x.Descricao)
        };

        var totalItems = await consulta.CountAsync(cancellationToken);
        var entidades = await consulta.ApplyPagination(query).ToListAsync(cancellationToken);
        var entidadesIds = entidades.Select(x => x.Id).ToArray();
        var contasPai = entidades
            .Where(x => x.ContaPaiId.HasValue)
            .Select(x => x.ContaPaiId!.Value)
            .Distinct()
            .ToArray();
        var contasComFilhos = await dbContext.ContasGerenciais.AsNoTracking()
            .Where(x => x.ContaPaiId.HasValue && entidadesIds.Contains(x.ContaPaiId.Value))
            .Select(x => x.ContaPaiId!.Value)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var descricoesPai = await dbContext.ContasGerenciais.AsNoTracking()
            .Where(x => contasPai.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Descricao, cancellationToken);
        var responsaveisPadraoIds = entidades
            .Where(x => x.ResponsavelPadraoId.HasValue)
            .Select(x => x.ResponsavelPadraoId!.Value)
            .Distinct()
            .ToArray();
        var responsaveisPadrao = await dbContext.Pessoas.AsNoTracking()
            .Where(x => responsaveisPadraoIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Nome, cancellationToken);
        var contasComFilhosSet = contasComFilhos.ToHashSet();
        var items = entidades
            .Select(x => new ContaGerencialResumoResponse(
                x.Id,
                x.Codigo,
                x.Descricao,
                MapearTipo(x.Tipo),
                x.ContaPaiId,
                x.ContaPaiId.HasValue && descricoesPai.TryGetValue(x.ContaPaiId.Value, out var descricaoPai)
                    ? descricaoPai
                    : null,
                x.ResponsavelPadraoId,
                x.ResponsavelPadraoId.HasValue && responsaveisPadrao.TryGetValue(x.ResponsavelPadraoId.Value, out var responsavelPadraoNome)
                    ? responsavelPadraoNome
                    : null,
                x.Ativo,
                !contasComFilhosSet.Contains(x.Id),
                x.EhPadraoRecebimentoFaturaCartao))
            .ToArray();

        return PagedResult<ContaGerencialResumoResponse>.Create(items, query.Page, query.PageSize, totalItems);
    }

    public async Task<ContaGerencialDetalheResponse?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasGerenciais.AsNoTracking()
            .Where(x => x.Id == id)
            .SingleOrDefaultAsync(cancellationToken);

        if (conta is null)
        {
            return null;
        }

        var contaPaiDescricao = conta.ContaPaiId.HasValue
            ? await dbContext.ContasGerenciais.AsNoTracking()
                .Where(parent => parent.Id == conta.ContaPaiId.Value)
                .Select(parent => parent.Descricao)
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        var responsavelPadraoNome = conta.ResponsavelPadraoId.HasValue
            ? await dbContext.Pessoas.AsNoTracking()
                .Where(pessoa => pessoa.Id == conta.ResponsavelPadraoId.Value)
                .Select(pessoa => pessoa.Nome)
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        var aceitaLancamentos = !await dbContext.ContasGerenciais.AsNoTracking()
            .AnyAsync(x => x.ContaPaiId == conta.Id, cancellationToken);

        return new ContaGerencialDetalheResponse(
            conta.Id,
            conta.Codigo,
            conta.Descricao,
            MapearTipo(conta.Tipo),
            conta.ContaPaiId,
            contaPaiDescricao,
            conta.ResponsavelPadraoId,
            responsavelPadraoNome,
            conta.Ativo,
            aceitaLancamentos,
            conta.EhPadraoRecebimentoFaturaCartao,
            conta.CreatedAtUtc,
            conta.UpdatedAtUtc);
    }

    public async Task<ContaGerencialDetalheResponse> CriarAsync(
        CriarContaGerencialRequest request,
        CancellationToken cancellationToken)
    {
        await ValidarHierarquiaAsync(null, request.ContaPaiId, cancellationToken);
        var tipoEfetivo = await ResolverTipoEfetivoAsync(request.Tipo, request.ContaPaiId, cancellationToken);
        await ValidarPadraoRecebimentoFaturaAsync(null, tipoEfetivo, request.EhPadraoRecebimentoFaturaCartao, cancellationToken);
        await ValidarResponsavelPadraoAsync(request.ResponsavelPadraoId, cancellationToken);

        ContaGerencial conta;

        try
        {
            conta = ContaGerencial.Criar(
                request.Codigo,
                request.Descricao,
                MapearTipo(tipoEfetivo),
                request.ContaPaiId,
                request.ResponsavelPadraoId,
                request.Ativo,
                request.EhPadraoRecebimentoFaturaCartao);
        }
        catch (ArgumentException exception)
        {
            throw ConverterParaValidacao(exception);
        }

        dbContext.ContasGerenciais.Add(conta);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await ObterPorIdAsync(conta.Id, cancellationToken)
            ?? throw new InvalidOperationException("Conta gerencial criada não foi encontrada.");
    }

    public async Task<ContaGerencialDetalheResponse?> AtualizarAsync(
        Guid id,
        AtualizarContaGerencialRequest request,
        CancellationToken cancellationToken)
    {
        var conta = await dbContext.ContasGerenciais.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (conta is null)
        {
            return null;
        }

        await ValidarHierarquiaAsync(id, request.ContaPaiId, cancellationToken);
        var tipoEfetivo = await ResolverTipoEfetivoAsync(request.Tipo, request.ContaPaiId, cancellationToken);
        await ValidarPadraoRecebimentoFaturaAsync(id, tipoEfetivo, request.EhPadraoRecebimentoFaturaCartao, cancellationToken);
        await ValidarResponsavelPadraoAsync(request.ResponsavelPadraoId, cancellationToken);

        try
        {
            conta.Atualizar(
                request.Codigo,
                request.Descricao,
                MapearTipo(tipoEfetivo),
                request.ContaPaiId,
                request.ResponsavelPadraoId,
                request.Ativo,
                request.EhPadraoRecebimentoFaturaCartao);
        }
        catch (ArgumentException exception)
        {
            throw ConverterParaValidacao(exception);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await ObterPorIdAsync(id, cancellationToken);
    }

    public async Task<SeedPlanoInicialResponse> SeedPlanoInicialAsync(CancellationToken cancellationToken)
    {
        var existentesPorCodigo = await dbContext.ContasGerenciais.AsNoTracking()
            .Where(x => x.Codigo != null)
            .ToDictionaryAsync(x => x.Codigo!, x => x.Id, cancellationToken);

        var criadas = 0;

        async Task<Guid> UpsertGrupoAsync(string codigo, string descricao, TipoContaGerencial tipo)
        {
            if (existentesPorCodigo.TryGetValue(codigo, out var idExistente))
                return idExistente;

            var conta = ContaGerencial.Criar(codigo, descricao, tipo, null, null, true, false);
            dbContext.ContasGerenciais.Add(conta);
            await dbContext.SaveChangesAsync(cancellationToken);
            existentesPorCodigo[codigo] = conta.Id;
            criadas++;
            return conta.Id;
        }

        void UpsertFilha(string codigoPai, string codigo, string descricao, TipoContaGerencial tipo)
        {
            if (existentesPorCodigo.ContainsKey(codigo))
                return;

            var paiId = existentesPorCodigo[codigoPai];
            var conta = ContaGerencial.Criar(codigo, descricao, tipo, paiId, null, true, false);
            dbContext.ContasGerenciais.Add(conta);
            existentesPorCodigo[codigo] = conta.Id;
            criadas++;
        }

        // ── Grupos raiz ──────────────────────────────────────────────
        await UpsertGrupoAsync("1",  "Renda do trabalho",           TipoContaGerencial.Receita);
        await UpsertGrupoAsync("2",  "Renda patrimonial e familiar", TipoContaGerencial.Receita);
        await UpsertGrupoAsync("3",  "Moradia",                      TipoContaGerencial.Despesa);
        await UpsertGrupoAsync("4",  "Imóvel locado",                TipoContaGerencial.Despesa);
        await UpsertGrupoAsync("5",  "Alimentação",                  TipoContaGerencial.Despesa);
        await UpsertGrupoAsync("6",  "Veículo",                      TipoContaGerencial.Despesa);
        await UpsertGrupoAsync("7",  "Saúde",                        TipoContaGerencial.Despesa);
        await UpsertGrupoAsync("8",  "Pet",                          TipoContaGerencial.Despesa);
        await UpsertGrupoAsync("9",  "Família e proteção",           TipoContaGerencial.Despesa);
        await UpsertGrupoAsync("10", "Pessoal e lazer",              TipoContaGerencial.Despesa);
        await UpsertGrupoAsync("11", "Encargos financeiros",         TipoContaGerencial.Despesa);
        await UpsertGrupoAsync("12", "Negócio",                      TipoContaGerencial.Despesa);
        await UpsertGrupoAsync("13", "Investimentos e poupança",     TipoContaGerencial.Despesa);

        // ── Contas filhas ─────────────────────────────────────────────
        // 1 — Renda do trabalho
        UpsertFilha("1", "1.1", "Salário",                TipoContaGerencial.Receita);
        UpsertFilha("1", "1.2", "13º salário e férias",   TipoContaGerencial.Receita);
        UpsertFilha("1", "1.3", "Recebimento de dívidas", TipoContaGerencial.Receita);
        // 2 — Renda patrimonial e familiar
        UpsertFilha("2", "2.1", "Aluguel recebido",  TipoContaGerencial.Receita);
        UpsertFilha("2", "2.2", "Aposentadoria — avô", TipoContaGerencial.Receita);
        // 3 — Moradia
        UpsertFilha("3", "3.1", "Aluguel pago",                        TipoContaGerencial.Despesa);
        UpsertFilha("3", "3.2", "Água",                                TipoContaGerencial.Despesa);
        UpsertFilha("3", "3.3", "Energia elétrica",                    TipoContaGerencial.Despesa);
        UpsertFilha("3", "3.4", "Internet",                            TipoContaGerencial.Despesa);
        UpsertFilha("3", "3.5", "Compras e manutenção do apartamento", TipoContaGerencial.Despesa);
        UpsertFilha("3", "3.6", "Gás",                                 TipoContaGerencial.Despesa);
        UpsertFilha("3", "3.7", "Faxineira e limpeza",                 TipoContaGerencial.Despesa);
        // 4 — Imóvel locado
        UpsertFilha("4", "4.1", "IPTU do imóvel",              TipoContaGerencial.Despesa);
        UpsertFilha("4", "4.2", "Condomínio do imóvel",        TipoContaGerencial.Despesa);
        UpsertFilha("4", "4.3", "Manutenção e reparos do imóvel", TipoContaGerencial.Despesa);
        // 5 — Alimentação
        UpsertFilha("5", "5.1", "Mercado e supermercado",    TipoContaGerencial.Despesa);
        UpsertFilha("5", "5.2", "Padaria",                   TipoContaGerencial.Despesa);
        UpsertFilha("5", "5.3", "Delivery",                  TipoContaGerencial.Despesa);
        UpsertFilha("5", "5.4", "Restaurante e refeições fora", TipoContaGerencial.Despesa);
        // 6 — Veículo
        UpsertFilha("6", "6.1", "Prestação do carro",       TipoContaGerencial.Despesa);
        UpsertFilha("6", "6.2", "Seguro do carro",          TipoContaGerencial.Despesa);
        UpsertFilha("6", "6.3", "Combustível",              TipoContaGerencial.Despesa);
        UpsertFilha("6", "6.4", "Manutenção e revisão",     TipoContaGerencial.Despesa);
        UpsertFilha("6", "6.5", "IPVA e licenciamento",     TipoContaGerencial.Despesa);
        UpsertFilha("6", "6.6", "Estacionamento e pedágio", TipoContaGerencial.Despesa);
        // 7 — Saúde
        UpsertFilha("7", "7.1", "Fisioterapia",                   TipoContaGerencial.Despesa);
        UpsertFilha("7", "7.2", "Farmácia e medicamentos",        TipoContaGerencial.Despesa);
        UpsertFilha("7", "7.3", "Plano de saúde",                 TipoContaGerencial.Despesa);
        UpsertFilha("7", "7.4", "Consultas e exames",             TipoContaGerencial.Despesa);
        UpsertFilha("7", "7.5", "Dentista e plano odontológico",  TipoContaGerencial.Despesa);
        // 8 — Pet
        UpsertFilha("8", "8.1", "Ração e suprimentos", TipoContaGerencial.Despesa);
        UpsertFilha("8", "8.2", "Veterinário",         TipoContaGerencial.Despesa);
        UpsertFilha("8", "8.3", "Banho e tosa",        TipoContaGerencial.Despesa);
        // 9 — Família e proteção
        UpsertFilha("9", "9.1", "Ajuda com parentes",         TipoContaGerencial.Despesa);
        UpsertFilha("9", "9.2", "Funerária e plano funeral",  TipoContaGerencial.Despesa);
        UpsertFilha("9", "9.3", "Presentes e datas especiais", TipoContaGerencial.Despesa);
        // 10 — Pessoal e lazer
        UpsertFilha("10", "10.1", "Vestuário e calçados",          TipoContaGerencial.Despesa);
        UpsertFilha("10", "10.2", "Higiene e cuidados pessoais",   TipoContaGerencial.Despesa);
        UpsertFilha("10", "10.3", "Streaming e assinaturas digitais", TipoContaGerencial.Despesa);
        UpsertFilha("10", "10.4", "Lazer e entretenimento",        TipoContaGerencial.Despesa);
        // 11 — Encargos financeiros
        UpsertFilha("11", "11.1", "Juros e encargos financeiros", TipoContaGerencial.Despesa);
        UpsertFilha("11", "11.2", "Tarifas bancárias",            TipoContaGerencial.Despesa);
        // 12 — Negócio
        UpsertFilha("12", "12.1", "Salário de funcionário",          TipoContaGerencial.Despesa);
        UpsertFilha("12", "12.2", "FGTS e encargos trabalhistas",    TipoContaGerencial.Despesa);
        UpsertFilha("12", "12.3", "Contabilidade",                   TipoContaGerencial.Despesa);
        UpsertFilha("12", "12.4", "Impostos e tributos",             TipoContaGerencial.Despesa);
        UpsertFilha("12", "12.5", "Material e suprimentos operacionais", TipoContaGerencial.Despesa);
        // 13 — Investimentos e poupança
        UpsertFilha("13", "13.1", "Reserva de emergência",          TipoContaGerencial.Despesa);
        UpsertFilha("13", "13.2", "Poupança e objetivos de curto prazo", TipoContaGerencial.Despesa);
        UpsertFilha("13", "13.3", "Renda fixa",                     TipoContaGerencial.Despesa);
        UpsertFilha("13", "13.4", "Previdência privada",            TipoContaGerencial.Despesa);
        UpsertFilha("13", "13.5", "Renda variável",                 TipoContaGerencial.Despesa);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new SeedPlanoInicialResponse(criadas);
    }

    private async Task ValidarHierarquiaAsync(Guid? contaId, Guid? contaPaiId, CancellationToken cancellationToken)
    {
        if (!contaPaiId.HasValue)
        {
            return;
        }

        if (contaId.HasValue && contaId.Value == contaPaiId.Value)
        {
            throw ValidationExceptionFactory.Create("ContaPaiId", "Conta pai não pode ser a própria conta.");
        }

        var existeContaPai = await dbContext.ContasGerenciais.AnyAsync(x => x.Id == contaPaiId.Value, cancellationToken);

        if (!existeContaPai)
        {
            throw ValidationExceptionFactory.Create("ContaPaiId", "Conta pai não encontrada.");
        }

        var proximaContaPai = await dbContext.ContasGerenciais
            .Where(x => x.Id == contaPaiId.Value)
            .Select(x => x.ContaPaiId)
            .SingleOrDefaultAsync(cancellationToken);

        while (proximaContaPai.HasValue)
        {
            if (contaId.HasValue && proximaContaPai.Value == contaId.Value)
            {
                throw ValidationExceptionFactory.Create("ContaPaiId", "A hierarquia informada gera ciclo.");
            }

            proximaContaPai = await dbContext.ContasGerenciais
                .Where(x => x.Id == proximaContaPai.Value)
                .Select(x => x.ContaPaiId)
                .SingleOrDefaultAsync(cancellationToken);
        }
    }

    private async Task<ContaGerencialTipo> ResolverTipoEfetivoAsync(
        ContaGerencialTipo tipoInformado,
        Guid? contaPaiId,
        CancellationToken cancellationToken)
    {
        if (!contaPaiId.HasValue)
        {
            return tipoInformado;
        }

        var tipoContaPai = await dbContext.ContasGerenciais.AsNoTracking()
            .Where(x => x.Id == contaPaiId.Value)
            .Select(x => x.Tipo)
            .SingleAsync(cancellationToken);

        return MapearTipo(tipoContaPai);
    }

    private async Task ValidarPadraoRecebimentoFaturaAsync(
        Guid? contaId,
        ContaGerencialTipo tipo,
        bool ehPadraoRecebimentoFaturaCartao,
        CancellationToken cancellationToken)
    {
        if (!ehPadraoRecebimentoFaturaCartao)
        {
            return;
        }

        if (tipo != ContaGerencialTipo.Receita)
        {
            throw ValidationExceptionFactory.Create(
                "EhPadraoRecebimentoFaturaCartao",
                "Somente contas gerenciais de receita podem ser marcadas como padrão de recebimento de fatura.");
        }

        var existeOutraContaPadrao = await dbContext.ContasGerenciais
            .AnyAsync(
                x => x.EhPadraoRecebimentoFaturaCartao &&
                     (!contaId.HasValue || x.Id != contaId.Value),
                cancellationToken);

        if (existeOutraContaPadrao)
        {
            throw ValidationExceptionFactory.Create(
                "EhPadraoRecebimentoFaturaCartao",
                "Já existe uma conta gerencial padrão para recebimento de fatura.");
        }
    }

    private async Task ValidarResponsavelPadraoAsync(Guid? responsavelPadraoId, CancellationToken cancellationToken)
    {
        if (!responsavelPadraoId.HasValue)
        {
            return;
        }

        var existeResponsavelPadrao = await dbContext.Pessoas
            .AnyAsync(x => x.Id == responsavelPadraoId.Value, cancellationToken);

        if (!existeResponsavelPadrao)
        {
            throw ValidationExceptionFactory.Create("ResponsavelPadraoId", "Responsável padrão não encontrado.");
        }
    }

    private static Exception ConverterParaValidacao(ArgumentException exception)
    {
        var campo = exception.ParamName switch
        {
            "descricao" => "Descricao",
            "contaPaiId" => "ContaPaiId",
            "ehPadraoRecebimentoFaturaCartao" => "EhPadraoRecebimentoFaturaCartao",
            "responsavelPadraoId" => "ResponsavelPadraoId",
            _ => "Request"
        };

        return ValidationExceptionFactory.Create(campo, exception.Message);
    }

    private static TipoContaGerencial MapearTipo(ContaGerencialTipo tipo)
    {
        return tipo switch
        {
            ContaGerencialTipo.Receita => TipoContaGerencial.Receita,
            ContaGerencialTipo.Despesa => TipoContaGerencial.Despesa,
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };
    }

    private static ContaGerencialTipo MapearTipo(TipoContaGerencial tipo)
    {
        return tipo switch
        {
            TipoContaGerencial.Receita => ContaGerencialTipo.Receita,
            TipoContaGerencial.Despesa => ContaGerencialTipo.Despesa,
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };
    }
}
