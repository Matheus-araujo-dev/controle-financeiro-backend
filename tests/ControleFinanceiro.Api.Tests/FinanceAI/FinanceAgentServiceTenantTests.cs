using System.Text.Json;
using ControleFinanceiro.Application.Common.Persistence;
using ControleFinanceiro.Application.FinanceAI;
using ControleFinanceiro.Application.FinanceAI.Tools;
using ControleFinanceiro.Domain.Cadastros.Pessoas;
using ControleFinanceiro.Domain.FinanceAI;
using ControleFinanceiro.Domain.Identidade;
using ControleFinanceiro.Infrastructure.Persistence;
using ControleFinanceiro.SharedKernel.Abstractions;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControleFinanceiro.Api.Tests.FinanceAI;

public sealed class FinanceAgentServiceTenantTests
{
    [Fact]
    public async Task ProcessarAsync_ComFamiliaInformada_DeveAplicarTenantNasFerramentas()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new AppDbContext(options, currentUser: AnonymousUser.Instance);
        await db.Database.EnsureCreatedAsync();

        var familiaA = Familia.Criar("Familia A");
        var familiaB = Familia.Criar("Familia B");
        var usuario = Usuario.Criar("google-sub", "usuario@teste.local", "Usuario", null);
        usuario.DefinirFamiliaAtiva(familiaA.Id);
        var usuarioId = usuario.Id;

        db.Familias.AddRange(familiaA, familiaB);
        db.Usuarios.Add(usuario);
        db.MembrosFamilia.Add(MembroFamilia.Criar(familiaA.Id, usuarioId, PapelFamilia.Administrador));

        var pessoaA = Pessoa.Criar("Pessoa A", TipoPessoa.Fisica, null, null, null, null, [], true);
        pessoaA.AtribuirFamilia(familiaA.Id);

        var pessoaB = Pessoa.Criar("Pessoa B", TipoPessoa.Fisica, null, null, null, null, [], true);
        pessoaB.AtribuirFamilia(familiaB.Id);

        db.Pessoas.AddRange(pessoaA, pessoaB);
        await db.SaveChangesAsync();

        var tool = new ContarPessoasTool(db);
        var llm = new SequencedLlmClient(
            new LlmCompletion(null, [new LlmToolCall(tool.Name, "{}", "tool-1")], new LlmUsage(1, 1), "tool_use"),
            new LlmCompletion("ok", [], new LlmUsage(1, 1), "end_turn"));

        var service = new FinanceAgentService(
            llm,
            db,
            AnonymousUser.Instance,
            [tool],
            NullLogger<FinanceAgentService>.Instance);

        await service.ProcessarAsync(
            new AgentRequest("conte as pessoas")
            {
                FamiliaId = familiaA.Id,
                UsuarioId = usuarioId,
                Canal = CanalAi.WhatsApp,
                ContatoExterno = "5531999999999"
            },
            CancellationToken.None);

        tool.TotalPessoasVisiveis.Should().Be(1);
    }

    private sealed class ContarPessoasTool(IAppDbContext db) : IFinanceTool
    {
        public int TotalPessoasVisiveis { get; private set; }

        public string Name => "contar_pessoas";

        public string Description => "Conta pessoas visiveis no tenant corrente.";

        public string InputSchema => """{"type":"object","properties":{}}""";

        public async Task<string> ExecuteAsync(string inputJson, ToolContext context, CancellationToken cancellationToken)
        {
            TotalPessoasVisiveis = await db.Pessoas.CountAsync(cancellationToken);
            return JsonSerializer.Serialize(new { total = TotalPessoasVisiveis });
        }
    }

    private sealed class SequencedLlmClient(params LlmCompletion[] completions) : ILlmClient
    {
        private readonly Queue<LlmCompletion> _completions = new(completions);

        public Task<LlmCompletion> CompleteAsync(LlmRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(_completions.Dequeue());
    }

    private sealed class AnonymousUser : ICurrentUser
    {
        public static readonly AnonymousUser Instance = new();

        public bool IsAuthenticated => false;

        public string? UserId => null;

        public Guid? WorkspaceId => null;

        public Guid? FamiliaId => WorkspaceId;

        public string? Papel => null;
    }
}


