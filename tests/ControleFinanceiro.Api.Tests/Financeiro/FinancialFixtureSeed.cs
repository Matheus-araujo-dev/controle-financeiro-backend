using System.Net.Http.Json;

namespace ControleFinanceiro.Api.Tests.Financeiro;

internal static class FinancialFixtureSeed
{
    public static async Task<FixtureIds> CreateAsync(HttpClient client)
    {
        var pagador = await CreatePessoaAsync(client, "Cliente Fase 3", "Fisica");
        var recebedor = await CreatePessoaAsync(client, "Fornecedor Fase 3", "Juridica");
        var responsavel = await CreatePessoaAsync(client, "Responsavel Fase 3", "Fisica");

        var formaManual = await CreateFormaPagamentoAsync(client, "Pix manual", "Pix", false, false);
        var formaAuto = await CreateFormaPagamentoAsync(client, "Debito automatico", "Debito", false, true);
        var formaCartao = await CreateFormaPagamentoAsync(client, "Cartao de credito", "Credito", true, false);

        var contaBancariaId = await CreateContaBancariaAsync(client);
        var cartaoId = await CreateCartaoAsync(client, contaBancariaId);
        var contaGerencialDespesaId = await CreateContaGerencialAsync(client, "DESP", "Despesa Operacional", "Despesa");
        var contaGerencialAdministrativaId = await CreateContaGerencialAsync(client, "ADM", "Administrativo", "Despesa");
        var contaGerencialReceitaId = await CreateContaGerencialAsync(client, "REC", "Receita de Servicos", "Receita");

        return new FixtureIds(
            PagadorId: pagador,
            RecebedorId: recebedor,
            ResponsavelId: responsavel,
            FormaPagamentoManualId: formaManual,
            FormaPagamentoAutoId: formaAuto,
            FormaPagamentoCartaoId: formaCartao,
            ContaBancariaId: contaBancariaId,
            CartaoId: cartaoId,
            ContaGerencialDespesaId: contaGerencialDespesaId,
            ContaGerencialAdministrativaId: contaGerencialAdministrativaId,
            ContaGerencialReceitaId: contaGerencialReceitaId);
    }

    private static async Task<Guid> CreatePessoaAsync(HttpClient client, string nome, string tipoPessoa)
    {
        var response = await client.PostAsJsonAsync("/api/v1/pessoas", new
        {
            nome,
            tipoPessoa
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<IdResponse>();
        return payload!.Id;
    }

    private static async Task<Guid> CreateFormaPagamentoAsync(
        HttpClient client,
        string nome,
        string tipo,
        bool ehCartao,
        bool baixarAutomaticamente)
    {
        var response = await client.PostAsJsonAsync("/api/v1/formas-pagamento", new
        {
            nome,
            tipo,
            ehCartao,
            baixarAutomaticamente,
            ativo = true
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<IdResponse>();
        return payload!.Id;
    }

    private static async Task<Guid> CreateContaBancariaAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/contas-bancarias", new
        {
            nome = "Conta operacional",
            banco = "Banco Exemplo",
            agencia = "0001",
            numeroConta = "12345-6",
            tipoConta = "Corrente",
            saldoInicial = 1000m,
            dataSaldoInicial = "2026-04-01",
            ativo = true
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<IdResponse>();
        return payload!.Id;
    }

    private static async Task<Guid> CreateCartaoAsync(HttpClient client, Guid contaBancariaId)
    {
        var response = await client.PostAsJsonAsync("/api/v1/cartoes", new
        {
            nome = "Cartao Fase 3",
            bandeira = "Visa",
            numeroFinal = "4242",
            diaFechamentoFatura = 10,
            diaVencimentoFatura = 20,
            contaBancariaPagamentoPadraoId = contaBancariaId,
            limiteCredito = 5000m,
            ativo = true
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<IdResponse>();
        return payload!.Id;
    }

    private static async Task<Guid> CreateContaGerencialAsync(HttpClient client, string codigo, string descricao, string tipo)
    {
        var response = await client.PostAsJsonAsync("/api/v1/contas-gerenciais", new
        {
            codigo,
            descricao,
            tipo,
            contaPaiId = (string?)null,
            ativo = true
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<IdResponse>();
        return payload!.Id;
    }

    internal sealed record FixtureIds(
        Guid PagadorId,
        Guid RecebedorId,
        Guid ResponsavelId,
        Guid FormaPagamentoManualId,
        Guid FormaPagamentoAutoId,
        Guid FormaPagamentoCartaoId,
        Guid ContaBancariaId,
        Guid CartaoId,
        Guid ContaGerencialDespesaId,
        Guid ContaGerencialAdministrativaId,
        Guid ContaGerencialReceitaId);

    private sealed record IdResponse(Guid Id);
}
