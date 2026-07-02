using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Domain.Cadastros.ContasGerenciais;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Application.Cadastros.ContasGerenciais;

public sealed class ContasGerenciaisPadraoSeedService(IAppDbContext dbContext)
{
    public async Task<int> SeedAsync(CancellationToken cancellationToken)
    {
        var existentesPorCodigo = await dbContext.ContasGerenciais
            .AsNoTracking()
            .Where(x => x.Codigo != null)
            .ToDictionaryAsync(x => x.Codigo!, x => x.Id, cancellationToken);

        var criadas = 0;

        async Task<Guid> RaizAsync(string codigo, string descricao, TipoContaGerencial tipo)
        {
            if (existentesPorCodigo.TryGetValue(codigo, out var id))
                return id;

            var c = ContaGerencial.Criar(codigo, descricao, tipo, null, null, true, false);
            dbContext.ContasGerenciais.Add(c);
            await dbContext.SaveChangesAsync(cancellationToken);
            existentesPorCodigo[codigo] = c.Id;
            criadas++;
            return c.Id;
        }

        async Task<Guid> GrupoAsync(string codigoPai, string codigo, string descricao, TipoContaGerencial tipo)
        {
            if (existentesPorCodigo.TryGetValue(codigo, out var id))
                return id;

            var paiId = existentesPorCodigo[codigoPai];
            var c = ContaGerencial.Criar(codigo, descricao, tipo, paiId, null, true, false);
            dbContext.ContasGerenciais.Add(c);
            await dbContext.SaveChangesAsync(cancellationToken);
            existentesPorCodigo[codigo] = c.Id;
            criadas++;
            return c.Id;
        }

        void Folha(string codigoPai, string codigo, string descricao, TipoContaGerencial tipo)
        {
            if (existentesPorCodigo.ContainsKey(codigo))
                return;

            var paiId = existentesPorCodigo[codigoPai];
            var c = ContaGerencial.Criar(codigo, descricao, tipo, paiId, null, true, false);
            dbContext.ContasGerenciais.Add(c);
            existentesPorCodigo[codigo] = c.Id;
            criadas++;
        }

        // ── Nível 1: raízes ──────────────────────────────────────────────────
        await RaizAsync("1", "Receitas", TipoContaGerencial.Receita);
        await RaizAsync("2", "Despesas", TipoContaGerencial.Despesa);

        // ── Nível 2: subgrupos de Receitas ───────────────────────────────────
        await GrupoAsync("1", "1.1", "Trabalho",        TipoContaGerencial.Receita);
        await GrupoAsync("1", "1.2", "Patrimônio",      TipoContaGerencial.Receita);
        await GrupoAsync("1", "1.3", "Outras receitas", TipoContaGerencial.Receita);

        // ── Nível 3: folhas de Receitas ──────────────────────────────────────
        // 1.1 Trabalho
        Folha("1.1", "1.1.1", "Salário / CLT",               TipoContaGerencial.Receita);
        Folha("1.1", "1.1.2", "Freelance / PJ / Pró-labore", TipoContaGerencial.Receita);
        Folha("1.1", "1.1.3", "Bônus e PLR",                 TipoContaGerencial.Receita);
        // 1.2 Patrimônio
        Folha("1.2", "1.2.1", "Aluguel recebido",            TipoContaGerencial.Receita);
        Folha("1.2", "1.2.2", "Rendimento de aplicações",    TipoContaGerencial.Receita);
        Folha("1.2", "1.2.3", "Dividendos e JCP",            TipoContaGerencial.Receita);
        // 1.3 Outras receitas
        Folha("1.3", "1.3.1", "Venda de bens",               TipoContaGerencial.Receita);
        Folha("1.3", "1.3.2", "Reembolsos e recebimentos",   TipoContaGerencial.Receita);
        Folha("1.3", "1.3.3", "Benefícios (FGTS, auxílios)", TipoContaGerencial.Receita);

        // ── Nível 2: subgrupos de Despesas ───────────────────────────────────
        await GrupoAsync("2", "2.1",  "Moradia",                  TipoContaGerencial.Despesa);
        await GrupoAsync("2", "2.2",  "Alimentação",              TipoContaGerencial.Despesa);
        await GrupoAsync("2", "2.3",  "Saúde e proteção",         TipoContaGerencial.Despesa);
        await GrupoAsync("2", "2.4",  "Bem-estar e beleza",       TipoContaGerencial.Despesa);
        await GrupoAsync("2", "2.5",  "Veículo",                  TipoContaGerencial.Despesa);
        await GrupoAsync("2", "2.6",  "Transporte urbano",        TipoContaGerencial.Despesa);
        await GrupoAsync("2", "2.7",  "Educação",                 TipoContaGerencial.Despesa);
        await GrupoAsync("2", "2.8",  "Vestuário",                TipoContaGerencial.Despesa);
        await GrupoAsync("2", "2.9",  "Pet",                      TipoContaGerencial.Despesa);
        await GrupoAsync("2", "2.10", "Assinaturas e serviços",   TipoContaGerencial.Despesa);
        await GrupoAsync("2", "2.11", "Lazer e entretenimento",   TipoContaGerencial.Despesa);
        await GrupoAsync("2", "2.12", "Família e apoio social",   TipoContaGerencial.Despesa);
        await GrupoAsync("2", "2.13", "Encargos financeiros",     TipoContaGerencial.Despesa);
        await GrupoAsync("2", "2.14", "Negócio / PJ",             TipoContaGerencial.Despesa);
        await GrupoAsync("2", "2.15", "Investimentos",            TipoContaGerencial.Despesa);

        // ── Nível 3: folhas de Despesas ──────────────────────────────────────
        // 2.1 Moradia
        Folha("2.1", "2.1.1", "Aluguel / prestação do imóvel",    TipoContaGerencial.Despesa);
        Folha("2.1", "2.1.2", "Água",                             TipoContaGerencial.Despesa);
        Folha("2.1", "2.1.3", "Energia elétrica",                 TipoContaGerencial.Despesa);
        Folha("2.1", "2.1.4", "Internet",                         TipoContaGerencial.Despesa);
        Folha("2.1", "2.1.5", "Condomínio",                       TipoContaGerencial.Despesa);
        Folha("2.1", "2.1.6", "IPTU",                             TipoContaGerencial.Despesa);
        Folha("2.1", "2.1.7", "Manutenção e reforma",             TipoContaGerencial.Despesa);
        Folha("2.1", "2.1.8", "Diarista / serviços domésticos",   TipoContaGerencial.Despesa);
        // 2.2 Alimentação
        Folha("2.2", "2.2.1", "Supermercado",                     TipoContaGerencial.Despesa);
        Folha("2.2", "2.2.2", "Padaria / feira",                  TipoContaGerencial.Despesa);
        Folha("2.2", "2.2.3", "Restaurante",                      TipoContaGerencial.Despesa);
        Folha("2.2", "2.2.4", "Delivery",                         TipoContaGerencial.Despesa);
        // 2.3 Saúde e proteção
        Folha("2.3", "2.3.1", "Plano de saúde",                   TipoContaGerencial.Despesa);
        Folha("2.3", "2.3.2", "Consultas médicas",                TipoContaGerencial.Despesa);
        Folha("2.3", "2.3.3", "Exames",                           TipoContaGerencial.Despesa);
        Folha("2.3", "2.3.4", "Farmácia e medicamentos",          TipoContaGerencial.Despesa);
        Folha("2.3", "2.3.5", "Dentista / plano odontológico",    TipoContaGerencial.Despesa);
        Folha("2.3", "2.3.6", "Academia / atividade física",      TipoContaGerencial.Despesa);
        Folha("2.3", "2.3.7", "Plano funerário",                  TipoContaGerencial.Despesa);
        // 2.4 Bem-estar e beleza
        Folha("2.4", "2.4.1", "Cabeleireiro / salão",             TipoContaGerencial.Despesa);
        Folha("2.4", "2.4.2", "Higiene e cosméticos",             TipoContaGerencial.Despesa);
        // 2.5 Veículo
        Folha("2.5", "2.5.1", "Combustível",                      TipoContaGerencial.Despesa);
        Folha("2.5", "2.5.2", "Seguro do carro",                  TipoContaGerencial.Despesa);
        Folha("2.5", "2.5.3", "Financiamento do carro",           TipoContaGerencial.Despesa);
        Folha("2.5", "2.5.4", "Manutenção e revisão",             TipoContaGerencial.Despesa);
        Folha("2.5", "2.5.5", "IPVA e licenciamento",             TipoContaGerencial.Despesa);
        // 2.6 Transporte urbano
        Folha("2.6", "2.6.1", "Uber / aplicativo de transporte",  TipoContaGerencial.Despesa);
        Folha("2.6", "2.6.2", "Ônibus / metrô / trem",           TipoContaGerencial.Despesa);
        // 2.7 Educação
        Folha("2.7", "2.7.1", "Escola / faculdade / mensalidade", TipoContaGerencial.Despesa);
        Folha("2.7", "2.7.2", "Cursos e capacitação",             TipoContaGerencial.Despesa);
        Folha("2.7", "2.7.3", "Material escolar / livros",        TipoContaGerencial.Despesa);
        // 2.8 Vestuário
        Folha("2.8", "2.8.1", "Roupas e calçados",                TipoContaGerencial.Despesa);
        Folha("2.8", "2.8.2", "Acessórios",                       TipoContaGerencial.Despesa);
        // 2.9 Pet
        Folha("2.9", "2.9.1", "Ração e suprimentos",              TipoContaGerencial.Despesa);
        Folha("2.9", "2.9.2", "Veterinário",                      TipoContaGerencial.Despesa);
        Folha("2.9", "2.9.3", "Banho e tosa",                     TipoContaGerencial.Despesa);
        // 2.10 Assinaturas e serviços
        Folha("2.10", "2.10.1", "Streaming (vídeo e música)",     TipoContaGerencial.Despesa);
        Folha("2.10", "2.10.2", "Software e ferramentas",         TipoContaGerencial.Despesa);
        Folha("2.10", "2.10.3", "Armazenamento em nuvem",         TipoContaGerencial.Despesa);
        Folha("2.10", "2.10.4", "Outras assinaturas",             TipoContaGerencial.Despesa);
        // 2.11 Lazer e entretenimento
        Folha("2.11", "2.11.1", "Cinema e shows",                 TipoContaGerencial.Despesa);
        Folha("2.11", "2.11.2", "Viagens e hospedagem",           TipoContaGerencial.Despesa);
        Folha("2.11", "2.11.3", "Jogos e hobbies",                TipoContaGerencial.Despesa);
        Folha("2.11", "2.11.4", "Presentes e datas especiais",    TipoContaGerencial.Despesa);
        // 2.12 Família e apoio social
        Folha("2.12", "2.12.1", "Ajuda a parentes",               TipoContaGerencial.Despesa);
        Folha("2.12", "2.12.2", "Pensão alimentar",               TipoContaGerencial.Despesa);
        Folha("2.12", "2.12.3", "Doações e caridade",             TipoContaGerencial.Despesa);
        // 2.13 Encargos financeiros
        Folha("2.13", "2.13.1", "Imposto de renda (IRPF)",        TipoContaGerencial.Despesa);
        Folha("2.13", "2.13.2", "Juros e encargos financeiros",   TipoContaGerencial.Despesa);
        Folha("2.13", "2.13.3", "Tarifas bancárias e IOF",        TipoContaGerencial.Despesa);
        // 2.14 Negócio / PJ
        Folha("2.14", "2.14.1", "Salário de funcionários",        TipoContaGerencial.Despesa);
        Folha("2.14", "2.14.2", "Contador e serviços contábeis",  TipoContaGerencial.Despesa);
        Folha("2.14", "2.14.3", "Impostos e tributos PJ",         TipoContaGerencial.Despesa);
        Folha("2.14", "2.14.4", "Despesas operacionais",          TipoContaGerencial.Despesa);
        // 2.15 Investimentos
        Folha("2.15", "2.15.1", "Reserva de emergência",          TipoContaGerencial.Despesa);
        Folha("2.15", "2.15.2", "Poupança e objetivos",           TipoContaGerencial.Despesa);
        Folha("2.15", "2.15.3", "Renda fixa (CDB, LCI, Tesouro)", TipoContaGerencial.Despesa);
        Folha("2.15", "2.15.4", "Previdência privada",            TipoContaGerencial.Despesa);
        Folha("2.15", "2.15.5", "Renda variável (ações, FIIs)",   TipoContaGerencial.Despesa);

        await dbContext.SaveChangesAsync(cancellationToken);

        return criadas;
    }
}
