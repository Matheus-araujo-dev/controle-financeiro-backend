using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ControleFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_conversas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Canal = table.Column<int>(type: "integer", nullable: false),
                    ContatoExterno = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    FamiliaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_conversas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AlertasWhatsappEnviados",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Telefone = table.Column<string>(type: "text", nullable: false),
                    TipoAlerta = table.Column<string>(type: "text", nullable: false),
                    ChaveReferencia = table.Column<string>(type: "text", nullable: false),
                    DataEnvio = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertasWhatsappEnviados", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "audit_trail_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExecutedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BeforeJson = table.Column<string>(type: "text", nullable: true),
                    AfterJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_trail_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "contas_bancarias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Banco = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Agencia = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    NumeroConta = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TipoConta = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SaldoInicial = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DataSaldoInicial = table.Column<DateOnly>(type: "date", nullable: false),
                    LimiteCartoesCompartilhado = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    FamiliaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contas_bancarias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "familias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_familias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "formas_pagamento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EhCartao = table.Column<bool>(type: "boolean", nullable: false),
                    BaixarAutomaticamente = table.Column<bool>(type: "boolean", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    FamiliaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_formas_pagamento", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "importacoes_whatsapp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoOrigem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Remetente = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TextoBruto = table.Column<string>(type: "text", nullable: true),
                    NomeArquivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CaminhoArquivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MimeType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ConfiancaExtracao = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    MensagemErro = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RecebidoEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessadoEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConfirmadoEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejeitadoEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    FamiliaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_importacoes_whatsapp", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "pessoas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TipoPessoa = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CpfCnpj = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Telefone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    FamiliaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pessoas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "regras_recorrencia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoLancamento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TipoPeriodicidade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TipoDia = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DiaOrdemMensal = table.Column<int>(type: "integer", nullable: false),
                    DataInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    DataFim = table.Column<DateOnly>(type: "date", nullable: true),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false),
                    PermiteEdicaoOcorrenciaIndividual = table.Column<bool>(type: "boolean", nullable: false),
                    Observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TemplateJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    FamiliaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regras_recorrencia", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "status_conta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Nome = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_status_conta", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "status_movimentacao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Nome = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_status_movimentacao", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GoogleSubject = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AvatarUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FamiliaAtivaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ai_mensagens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Papel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Conteudo = table.Column<string>(type: "text", nullable: false),
                    ExternalMessageId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_mensagens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_mensagens_ai_conversas_ConversaId",
                        column: x => x.ConversaId,
                        principalTable: "ai_conversas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_tool_calls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversaId = table.Column<Guid>(type: "uuid", nullable: false),
                    NomeFerramenta = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InputJson = table.Column<string>(type: "text", nullable: false),
                    OutputJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TokensEntrada = table.Column<int>(type: "integer", nullable: false),
                    TokensSaida = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_tool_calls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_tool_calls_ai_conversas_ConversaId",
                        column: x => x.ConversaId,
                        principalTable: "ai_conversas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cartoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Bandeira = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NumeroFinal = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    DiaFechamentoFatura = table.Column<int>(type: "integer", nullable: false),
                    DiaVencimentoFatura = table.Column<int>(type: "integer", nullable: false),
                    ContaBancariaPagamentoPadraoId = table.Column<Guid>(type: "uuid", nullable: true),
                    LimiteCredito = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    FamiliaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cartoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cartoes_contas_bancarias_ContaBancariaPagamentoPadraoId",
                        column: x => x.ContaBancariaPagamentoPadraoId,
                        principalTable: "contas_bancarias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "convites_familia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FamiliaId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailConvidado = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Papel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiraEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    UsuarioAceiteId = table.Column<Guid>(type: "uuid", nullable: true),
                    AceitoEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_convites_familia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_convites_familia_familias_FamiliaId",
                        column: x => x.FamiliaId,
                        principalTable: "familias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contas_gerenciais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ContaPaiId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResponsavelPadraoId = table.Column<Guid>(type: "uuid", nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    EhPadraoRecebimentoFaturaCartao = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    FamiliaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contas_gerenciais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contas_gerenciais_contas_gerenciais_ContaPaiId",
                        column: x => x.ContaPaiId,
                        principalTable: "contas_gerenciais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contas_gerenciais_pessoas_ResponsavelPadraoId",
                        column: x => x.ResponsavelPadraoId,
                        principalTable: "pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pessoas_chaves_pix",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PessoaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Chave = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pessoas_chaves_pix", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pessoas_chaves_pix_pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "membros_familia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FamiliaId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Papel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_membros_familia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_membros_familia_familias_FamiliaId",
                        column: x => x.FamiliaId,
                        principalTable: "familias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_membros_familia_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiraEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevogadoEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubstituidoPorTokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "whatsapp_config_alertas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceberVencimento = table.Column<bool>(type: "boolean", nullable: false),
                    DiasAntecedenciaVencimento = table.Column<int>(type: "integer", nullable: false),
                    ReceberLimiteCategoria = table.Column<bool>(type: "boolean", nullable: false),
                    ReceberLimiteResponsavel = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    FamiliaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_config_alertas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_whatsapp_config_alertas_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "whatsapp_usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    VerificadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    FamiliaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_usuarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_whatsapp_usuarios_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contas_receber",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NumeroDocumento = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    DataEmissao = table.Column<DateOnly>(type: "date", nullable: false),
                    ResponsavelId = table.Column<Guid>(type: "uuid", nullable: true),
                    PagadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataVencimento = table.Column<DateOnly>(type: "date", nullable: false),
                    DataLiquidacao = table.Column<DateOnly>(type: "date", nullable: true),
                    FormaPagamentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    CartaoId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContaBancariaId = table.Column<Guid>(type: "uuid", nullable: true),
                    ValorOriginal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorDesconto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorJuros = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorMulta = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorLiquido = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    QuantidadeParcelas = table.Column<int>(type: "integer", nullable: false),
                    NumeroParcela = table.Column<int>(type: "integer", nullable: false),
                    GrupoParcelamentoId = table.Column<Guid>(type: "uuid", nullable: true),
                    Descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    StatusContaId = table.Column<Guid>(type: "uuid", nullable: false),
                    EhRecorrente = table.Column<bool>(type: "boolean", nullable: false),
                    RegraRecorrenciaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Origem = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    FamiliaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contas_receber", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contas_receber_cartoes_CartaoId",
                        column: x => x.CartaoId,
                        principalTable: "cartoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contas_receber_contas_bancarias_ContaBancariaId",
                        column: x => x.ContaBancariaId,
                        principalTable: "contas_bancarias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contas_receber_formas_pagamento_FormaPagamentoId",
                        column: x => x.FormaPagamentoId,
                        principalTable: "formas_pagamento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contas_receber_pessoas_PagadorId",
                        column: x => x.PagadorId,
                        principalTable: "pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contas_receber_pessoas_ResponsavelId",
                        column: x => x.ResponsavelId,
                        principalTable: "pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contas_receber_regras_recorrencia_RegraRecorrenciaId",
                        column: x => x.RegraRecorrenciaId,
                        principalTable: "regras_recorrencia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contas_receber_status_conta_StatusContaId",
                        column: x => x.StatusContaId,
                        principalTable: "status_conta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "faturas_cartao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CartaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Competencia = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    DataFechamento = table.Column<DateOnly>(type: "date", nullable: false),
                    DataVencimento = table.Column<DateOnly>(type: "date", nullable: false),
                    ValorTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DataPagamento = table.Column<DateOnly>(type: "date", nullable: true),
                    ContaBancariaPagamentoId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    FamiliaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_faturas_cartao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_faturas_cartao_cartoes_CartaoId",
                        column: x => x.CartaoId,
                        principalTable: "cartoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_faturas_cartao_contas_bancarias_ContaBancariaPagamentoId",
                        column: x => x.ContaBancariaPagamentoId,
                        principalTable: "contas_bancarias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "metas_orcamento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContaGerencialId = table.Column<Guid>(type: "uuid", nullable: false),
                    Competencia = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    ValorMeta = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    FamiliaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_metas_orcamento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_metas_orcamento_contas_gerenciais_ContaGerencialId",
                        column: x => x.ContaGerencialId,
                        principalTable: "contas_gerenciais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "itens_importados_whatsapp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportacaoWhatsappId = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoSugestao = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PayloadSugeridoJson = table.Column<string>(type: "text", nullable: false),
                    ChaveAprendizado = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContaGerencialId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResponsavelId = table.Column<Guid>(type: "uuid", nullable: true),
                    DescricaoAjustada = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MarcarComoRecorrente = table.Column<bool>(type: "boolean", nullable: false),
                    ContaReceberId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ConfirmadoEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejeitadoEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    FamiliaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_itens_importados_whatsapp", x => x.Id);
                    table.ForeignKey(
                        name: "FK_itens_importados_whatsapp_contas_gerenciais_ContaGerencialId",
                        column: x => x.ContaGerencialId,
                        principalTable: "contas_gerenciais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_itens_importados_whatsapp_contas_receber_ContaReceberId",
                        column: x => x.ContaReceberId,
                        principalTable: "contas_receber",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_itens_importados_whatsapp_importacoes_whatsapp_ImportacaoWh~",
                        column: x => x.ImportacaoWhatsappId,
                        principalTable: "importacoes_whatsapp",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_itens_importados_whatsapp_pessoas_ResponsavelId",
                        column: x => x.ResponsavelId,
                        principalTable: "pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "compras_planejadas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Titulo = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ValorEstimado = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DataDesejada = table.Column<DateOnly>(type: "date", nullable: true),
                    Prioridade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Parcelavel = table.Column<bool>(type: "boolean", nullable: false),
                    QuantidadeParcelasDesejada = table.Column<int>(type: "integer", nullable: true),
                    ContaGerencialId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResponsavelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Link = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ContaPagarGeradaId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConvertidaEmContaPagarEmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    FamiliaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compras_planejadas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_compras_planejadas_contas_gerenciais_ContaGerencialId",
                        column: x => x.ContaGerencialId,
                        principalTable: "contas_gerenciais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_compras_planejadas_pessoas_ResponsavelId",
                        column: x => x.ResponsavelId,
                        principalTable: "pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contas_pagar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NumeroDocumento = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    DataEmissao = table.Column<DateOnly>(type: "date", nullable: false),
                    ResponsavelCompraId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecebedorId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataVencimento = table.Column<DateOnly>(type: "date", nullable: false),
                    DataLiquidacao = table.Column<DateOnly>(type: "date", nullable: true),
                    FormaPagamentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    CartaoId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContaBancariaId = table.Column<Guid>(type: "uuid", nullable: true),
                    ValorOriginal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorDesconto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorJuros = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorMulta = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorLiquido = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    QuantidadeParcelas = table.Column<int>(type: "integer", nullable: false),
                    NumeroParcela = table.Column<int>(type: "integer", nullable: false),
                    GrupoParcelamentoId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrigemCompraPlanejadaId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrigemImportacaoWhatsappId = table.Column<Guid>(type: "uuid", nullable: true),
                    FaturaCartaoId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChaveSerieImportacaoCartao = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: true),
                    Descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    StatusContaId = table.Column<Guid>(type: "uuid", nullable: false),
                    EhRecorrente = table.Column<bool>(type: "boolean", nullable: false),
                    RegraRecorrenciaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Origem = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    FamiliaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contas_pagar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contas_pagar_cartoes_CartaoId",
                        column: x => x.CartaoId,
                        principalTable: "cartoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contas_pagar_compras_planejadas_OrigemCompraPlanejadaId",
                        column: x => x.OrigemCompraPlanejadaId,
                        principalTable: "compras_planejadas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contas_pagar_contas_bancarias_ContaBancariaId",
                        column: x => x.ContaBancariaId,
                        principalTable: "contas_bancarias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contas_pagar_faturas_cartao_FaturaCartaoId",
                        column: x => x.FaturaCartaoId,
                        principalTable: "faturas_cartao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contas_pagar_formas_pagamento_FormaPagamentoId",
                        column: x => x.FormaPagamentoId,
                        principalTable: "formas_pagamento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contas_pagar_importacoes_whatsapp_OrigemImportacaoWhatsappId",
                        column: x => x.OrigemImportacaoWhatsappId,
                        principalTable: "importacoes_whatsapp",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contas_pagar_pessoas_RecebedorId",
                        column: x => x.RecebedorId,
                        principalTable: "pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contas_pagar_pessoas_ResponsavelCompraId",
                        column: x => x.ResponsavelCompraId,
                        principalTable: "pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contas_pagar_regras_recorrencia_RegraRecorrenciaId",
                        column: x => x.RegraRecorrenciaId,
                        principalTable: "regras_recorrencia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contas_pagar_status_conta_StatusContaId",
                        column: x => x.StatusContaId,
                        principalTable: "status_conta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "movimentacoes_financeiras",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DataMovimentacao = table.Column<DateOnly>(type: "date", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Natureza = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ContaBancariaId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContaPagarId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContaReceberId = table.Column<Guid>(type: "uuid", nullable: true),
                    FaturaCartaoId = table.Column<Guid>(type: "uuid", nullable: true),
                    Valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    StatusMovimentacaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    FamiliaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movimentacoes_financeiras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_movimentacoes_financeiras_contas_bancarias_ContaBancariaId",
                        column: x => x.ContaBancariaId,
                        principalTable: "contas_bancarias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_movimentacoes_financeiras_contas_pagar_ContaPagarId",
                        column: x => x.ContaPagarId,
                        principalTable: "contas_pagar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_movimentacoes_financeiras_contas_receber_ContaReceberId",
                        column: x => x.ContaReceberId,
                        principalTable: "contas_receber",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_movimentacoes_financeiras_faturas_cartao_FaturaCartaoId",
                        column: x => x.FaturaCartaoId,
                        principalTable: "faturas_cartao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_movimentacoes_financeiras_status_movimentacao_StatusMovimen~",
                        column: x => x.StatusMovimentacaoId,
                        principalTable: "status_movimentacao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rateios_conta_gerencial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoLancamento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ContaPagarId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContaReceberId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContaGerencialId = table.Column<Guid>(type: "uuid", nullable: false),
                    Percentual = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    Valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    FamiliaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rateios_conta_gerencial", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rateios_conta_gerencial_contas_gerenciais_ContaGerencialId",
                        column: x => x.ContaGerencialId,
                        principalTable: "contas_gerenciais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rateios_conta_gerencial_contas_pagar_ContaPagarId",
                        column: x => x.ContaPagarId,
                        principalTable: "contas_pagar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_rateios_conta_gerencial_contas_receber_ContaReceberId",
                        column: x => x.ContaReceberId,
                        principalTable: "contas_receber",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "status_conta",
                columns: new[] { "Id", "Codigo", "Nome" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), "PENDENTE", "Pendente" },
                    { new Guid("22222222-2222-2222-2222-222222222222"), "LIQUIDADA", "Liquidada" },
                    { new Guid("33333333-3333-3333-3333-333333333333"), "VENCIDA", "Vencida" },
                    { new Guid("44444444-4444-4444-4444-444444444444"), "CANCELADA", "Cancelada" },
                    { new Guid("55555555-5555-5555-5555-555555555555"), "PARCIAL", "Parcial" },
                    { new Guid("66666666-6666-6666-6666-666666666666"), "EM_FATURA", "Em fatura" }
                });

            migrationBuilder.InsertData(
                table: "status_movimentacao",
                columns: new[] { "Id", "Codigo", "Nome" },
                values: new object[,]
                {
                    { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"), "PREVISTA", "Prevista" },
                    { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2"), "EFETIVADA", "Efetivada" },
                    { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa4"), "CANCELADA", "Cancelada" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_conversas_FamiliaId",
                table: "ai_conversas",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_conversas_UsuarioId",
                table: "ai_conversas",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_mensagens_ConversaId",
                table: "ai_mensagens",
                column: "ConversaId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_mensagens_ExternalMessageId",
                table: "ai_mensagens",
                column: "ExternalMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_tool_calls_ConversaId",
                table: "ai_tool_calls",
                column: "ConversaId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_trail_entries_EntityName_EntityId",
                table: "audit_trail_entries",
                columns: new[] { "EntityName", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_cartoes_ContaBancariaPagamentoPadraoId",
                table: "cartoes",
                column: "ContaBancariaPagamentoPadraoId");

            migrationBuilder.CreateIndex(
                name: "IX_cartoes_FamiliaId",
                table: "cartoes",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_compras_planejadas_ContaGerencialId",
                table: "compras_planejadas",
                column: "ContaGerencialId");

            migrationBuilder.CreateIndex(
                name: "IX_compras_planejadas_ContaPagarGeradaId",
                table: "compras_planejadas",
                column: "ContaPagarGeradaId");

            migrationBuilder.CreateIndex(
                name: "IX_compras_planejadas_FamiliaId",
                table: "compras_planejadas",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_compras_planejadas_ResponsavelId",
                table: "compras_planejadas",
                column: "ResponsavelId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_bancarias_FamiliaId",
                table: "contas_bancarias",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_gerenciais_Codigo",
                table: "contas_gerenciais",
                column: "Codigo",
                unique: true,
                filter: "\"Codigo\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_contas_gerenciais_ContaPaiId",
                table: "contas_gerenciais",
                column: "ContaPaiId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_gerenciais_FamiliaId",
                table: "contas_gerenciais",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_gerenciais_ResponsavelPadraoId",
                table: "contas_gerenciais",
                column: "ResponsavelPadraoId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_gerenciais_Tipo_Ativo",
                table: "contas_gerenciais",
                columns: new[] { "Tipo", "Ativo" });

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_CartaoId_ChaveSerieImportacaoCartao_NumeroParc~",
                table: "contas_pagar",
                columns: new[] { "CartaoId", "ChaveSerieImportacaoCartao", "NumeroParcela", "QuantidadeParcelas" },
                filter: "\"CartaoId\" IS NOT NULL AND \"ChaveSerieImportacaoCartao\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_ContaBancariaId",
                table: "contas_pagar",
                column: "ContaBancariaId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_DataEmissao",
                table: "contas_pagar",
                column: "DataEmissao");

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_DataVencimento",
                table: "contas_pagar",
                column: "DataVencimento");

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_Descricao",
                table: "contas_pagar",
                column: "Descricao");

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_FamiliaId",
                table: "contas_pagar",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_FaturaCartaoId",
                table: "contas_pagar",
                column: "FaturaCartaoId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_FormaPagamentoId",
                table: "contas_pagar",
                column: "FormaPagamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_GrupoParcelamentoId",
                table: "contas_pagar",
                column: "GrupoParcelamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_NumeroDocumento",
                table: "contas_pagar",
                column: "NumeroDocumento");

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_OrigemCompraPlanejadaId",
                table: "contas_pagar",
                column: "OrigemCompraPlanejadaId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_OrigemImportacaoWhatsappId",
                table: "contas_pagar",
                column: "OrigemImportacaoWhatsappId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_RecebedorId_StatusContaId",
                table: "contas_pagar",
                columns: new[] { "RecebedorId", "StatusContaId" });

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_RegraRecorrenciaId",
                table: "contas_pagar",
                column: "RegraRecorrenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_ResponsavelCompraId",
                table: "contas_pagar",
                column: "ResponsavelCompraId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_StatusContaId_DataVencimento",
                table: "contas_pagar",
                columns: new[] { "StatusContaId", "DataVencimento" });

            migrationBuilder.CreateIndex(
                name: "IX_contas_receber_CartaoId",
                table: "contas_receber",
                column: "CartaoId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_receber_ContaBancariaId",
                table: "contas_receber",
                column: "ContaBancariaId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_receber_DataEmissao",
                table: "contas_receber",
                column: "DataEmissao");

            migrationBuilder.CreateIndex(
                name: "IX_contas_receber_DataVencimento",
                table: "contas_receber",
                column: "DataVencimento");

            migrationBuilder.CreateIndex(
                name: "IX_contas_receber_FamiliaId",
                table: "contas_receber",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_receber_FormaPagamentoId",
                table: "contas_receber",
                column: "FormaPagamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_receber_PagadorId_StatusContaId",
                table: "contas_receber",
                columns: new[] { "PagadorId", "StatusContaId" });

            migrationBuilder.CreateIndex(
                name: "IX_contas_receber_RegraRecorrenciaId",
                table: "contas_receber",
                column: "RegraRecorrenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_receber_ResponsavelId",
                table: "contas_receber",
                column: "ResponsavelId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_receber_StatusContaId_DataVencimento",
                table: "contas_receber",
                columns: new[] { "StatusContaId", "DataVencimento" });

            migrationBuilder.CreateIndex(
                name: "IX_convites_familia_FamiliaId",
                table: "convites_familia",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_convites_familia_TokenHash",
                table: "convites_familia",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_faturas_cartao_CartaoId_Competencia",
                table: "faturas_cartao",
                columns: new[] { "CartaoId", "Competencia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_faturas_cartao_ContaBancariaPagamentoId",
                table: "faturas_cartao",
                column: "ContaBancariaPagamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_faturas_cartao_FamiliaId",
                table: "faturas_cartao",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_formas_pagamento_FamiliaId",
                table: "formas_pagamento",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_importacoes_whatsapp_FamiliaId",
                table: "importacoes_whatsapp",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_importacoes_whatsapp_Status",
                table: "importacoes_whatsapp",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_itens_importados_whatsapp_ChaveAprendizado",
                table: "itens_importados_whatsapp",
                column: "ChaveAprendizado");

            migrationBuilder.CreateIndex(
                name: "IX_itens_importados_whatsapp_ContaGerencialId",
                table: "itens_importados_whatsapp",
                column: "ContaGerencialId");

            migrationBuilder.CreateIndex(
                name: "IX_itens_importados_whatsapp_ContaReceberId",
                table: "itens_importados_whatsapp",
                column: "ContaReceberId");

            migrationBuilder.CreateIndex(
                name: "IX_itens_importados_whatsapp_FamiliaId",
                table: "itens_importados_whatsapp",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_itens_importados_whatsapp_ImportacaoWhatsappId",
                table: "itens_importados_whatsapp",
                column: "ImportacaoWhatsappId");

            migrationBuilder.CreateIndex(
                name: "IX_itens_importados_whatsapp_ResponsavelId",
                table: "itens_importados_whatsapp",
                column: "ResponsavelId");

            migrationBuilder.CreateIndex(
                name: "IX_membros_familia_FamiliaId_UsuarioId",
                table: "membros_familia",
                columns: new[] { "FamiliaId", "UsuarioId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_membros_familia_UsuarioId",
                table: "membros_familia",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_metas_orcamento_ContaGerencialId",
                table: "metas_orcamento",
                column: "ContaGerencialId");

            migrationBuilder.CreateIndex(
                name: "IX_metas_orcamento_FamiliaId",
                table: "metas_orcamento",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_metas_orcamento_FamiliaId_ContaGerencialId_Competencia",
                table: "metas_orcamento",
                columns: new[] { "FamiliaId", "ContaGerencialId", "Competencia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_movimentacoes_financeiras_ContaBancariaId_DataMovimentacao",
                table: "movimentacoes_financeiras",
                columns: new[] { "ContaBancariaId", "DataMovimentacao" });

            migrationBuilder.CreateIndex(
                name: "IX_movimentacoes_financeiras_ContaPagarId",
                table: "movimentacoes_financeiras",
                column: "ContaPagarId");

            migrationBuilder.CreateIndex(
                name: "IX_movimentacoes_financeiras_ContaReceberId",
                table: "movimentacoes_financeiras",
                column: "ContaReceberId");

            migrationBuilder.CreateIndex(
                name: "IX_movimentacoes_financeiras_DataMovimentacao",
                table: "movimentacoes_financeiras",
                column: "DataMovimentacao");

            migrationBuilder.CreateIndex(
                name: "IX_movimentacoes_financeiras_FamiliaId",
                table: "movimentacoes_financeiras",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_movimentacoes_financeiras_FaturaCartaoId",
                table: "movimentacoes_financeiras",
                column: "FaturaCartaoId");

            migrationBuilder.CreateIndex(
                name: "IX_movimentacoes_financeiras_Natureza_StatusMovimentacaoId_Dat~",
                table: "movimentacoes_financeiras",
                columns: new[] { "Natureza", "StatusMovimentacaoId", "DataMovimentacao" });

            migrationBuilder.CreateIndex(
                name: "IX_movimentacoes_financeiras_StatusMovimentacaoId_DataMoviment~",
                table: "movimentacoes_financeiras",
                columns: new[] { "StatusMovimentacaoId", "DataMovimentacao" });

            migrationBuilder.CreateIndex(
                name: "IX_pessoas_Ativo_Nome",
                table: "pessoas",
                columns: new[] { "Ativo", "Nome" });

            migrationBuilder.CreateIndex(
                name: "IX_pessoas_CpfCnpj",
                table: "pessoas",
                column: "CpfCnpj",
                unique: true,
                filter: "\"CpfCnpj\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_pessoas_FamiliaId",
                table: "pessoas",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_pessoas_Nome",
                table: "pessoas",
                column: "Nome");

            migrationBuilder.CreateIndex(
                name: "IX_pessoas_chaves_pix_PessoaId_Tipo_Chave",
                table: "pessoas_chaves_pix",
                columns: new[] { "PessoaId", "Tipo", "Chave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rateios_conta_gerencial_ContaGerencialId",
                table: "rateios_conta_gerencial",
                column: "ContaGerencialId");

            migrationBuilder.CreateIndex(
                name: "IX_rateios_conta_gerencial_ContaPagarId",
                table: "rateios_conta_gerencial",
                column: "ContaPagarId");

            migrationBuilder.CreateIndex(
                name: "IX_rateios_conta_gerencial_ContaReceberId",
                table: "rateios_conta_gerencial",
                column: "ContaReceberId");

            migrationBuilder.CreateIndex(
                name: "IX_rateios_conta_gerencial_FamiliaId",
                table: "rateios_conta_gerencial",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_TokenHash",
                table: "refresh_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_UsuarioId",
                table: "refresh_tokens",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_regras_recorrencia_FamiliaId",
                table: "regras_recorrencia",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_regras_recorrencia_TipoLancamento_Ativa",
                table: "regras_recorrencia",
                columns: new[] { "TipoLancamento", "Ativa" });

            migrationBuilder.CreateIndex(
                name: "IX_status_conta_Codigo",
                table: "status_conta",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_status_movimentacao_Codigo",
                table: "status_movimentacao",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_Email",
                table: "usuarios",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_GoogleSubject",
                table: "usuarios",
                column: "GoogleSubject",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_config_alertas_FamiliaId",
                table: "whatsapp_config_alertas",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_config_alertas_UsuarioId",
                table: "whatsapp_config_alertas",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_usuarios_FamiliaId",
                table: "whatsapp_usuarios",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_usuarios_Telefone",
                table: "whatsapp_usuarios",
                column: "Telefone");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_usuarios_UsuarioId",
                table: "whatsapp_usuarios",
                column: "UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_compras_planejadas_contas_pagar_ContaPagarGeradaId",
                table: "compras_planejadas",
                column: "ContaPagarGeradaId",
                principalTable: "contas_pagar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cartoes_contas_bancarias_ContaBancariaPagamentoPadraoId",
                table: "cartoes");

            migrationBuilder.DropForeignKey(
                name: "FK_contas_pagar_contas_bancarias_ContaBancariaId",
                table: "contas_pagar");

            migrationBuilder.DropForeignKey(
                name: "FK_faturas_cartao_contas_bancarias_ContaBancariaPagamentoId",
                table: "faturas_cartao");

            migrationBuilder.DropForeignKey(
                name: "FK_compras_planejadas_contas_gerenciais_ContaGerencialId",
                table: "compras_planejadas");

            migrationBuilder.DropForeignKey(
                name: "FK_compras_planejadas_contas_pagar_ContaPagarGeradaId",
                table: "compras_planejadas");

            migrationBuilder.DropTable(
                name: "ai_mensagens");

            migrationBuilder.DropTable(
                name: "ai_tool_calls");

            migrationBuilder.DropTable(
                name: "AlertasWhatsappEnviados");

            migrationBuilder.DropTable(
                name: "audit_trail_entries");

            migrationBuilder.DropTable(
                name: "convites_familia");

            migrationBuilder.DropTable(
                name: "itens_importados_whatsapp");

            migrationBuilder.DropTable(
                name: "membros_familia");

            migrationBuilder.DropTable(
                name: "metas_orcamento");

            migrationBuilder.DropTable(
                name: "movimentacoes_financeiras");

            migrationBuilder.DropTable(
                name: "pessoas_chaves_pix");

            migrationBuilder.DropTable(
                name: "rateios_conta_gerencial");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "whatsapp_config_alertas");

            migrationBuilder.DropTable(
                name: "whatsapp_usuarios");

            migrationBuilder.DropTable(
                name: "ai_conversas");

            migrationBuilder.DropTable(
                name: "familias");

            migrationBuilder.DropTable(
                name: "status_movimentacao");

            migrationBuilder.DropTable(
                name: "contas_receber");

            migrationBuilder.DropTable(
                name: "usuarios");

            migrationBuilder.DropTable(
                name: "contas_bancarias");

            migrationBuilder.DropTable(
                name: "contas_gerenciais");

            migrationBuilder.DropTable(
                name: "contas_pagar");

            migrationBuilder.DropTable(
                name: "compras_planejadas");

            migrationBuilder.DropTable(
                name: "faturas_cartao");

            migrationBuilder.DropTable(
                name: "formas_pagamento");

            migrationBuilder.DropTable(
                name: "importacoes_whatsapp");

            migrationBuilder.DropTable(
                name: "regras_recorrencia");

            migrationBuilder.DropTable(
                name: "status_conta");

            migrationBuilder.DropTable(
                name: "pessoas");

            migrationBuilder.DropTable(
                name: "cartoes");
        }
    }
}
