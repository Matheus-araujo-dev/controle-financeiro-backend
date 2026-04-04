using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ControleFinanceiro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase3NucleoFinanceiro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "status_conta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_status_conta", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "status_movimentacao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_status_movimentacao", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "contas_pagar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NumeroDocumento = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    DataEmissao = table.Column<DateOnly>(type: "date", nullable: false),
                    ResponsavelCompraId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecebedorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataVencimento = table.Column<DateOnly>(type: "date", nullable: false),
                    DataLiquidacao = table.Column<DateOnly>(type: "date", nullable: true),
                    FormaPagamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CartaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContaBancariaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ValorOriginal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorDesconto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorJuros = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorMulta = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorLiquido = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    QuantidadeParcelas = table.Column<int>(type: "int", nullable: false),
                    NumeroParcela = table.Column<int>(type: "int", nullable: false),
                    GrupoParcelamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    StatusContaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EhRecorrente = table.Column<bool>(type: "bit", nullable: false),
                    RegraRecorrenciaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Origem = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
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
                        name: "FK_contas_pagar_contas_bancarias_ContaBancariaId",
                        column: x => x.ContaBancariaId,
                        principalTable: "contas_bancarias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contas_pagar_formas_pagamento_FormaPagamentoId",
                        column: x => x.FormaPagamentoId,
                        principalTable: "formas_pagamento",
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
                        name: "FK_contas_pagar_status_conta_StatusContaId",
                        column: x => x.StatusContaId,
                        principalTable: "status_conta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contas_receber",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NumeroDocumento = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    DataEmissao = table.Column<DateOnly>(type: "date", nullable: false),
                    ResponsavelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PagadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataVencimento = table.Column<DateOnly>(type: "date", nullable: false),
                    DataLiquidacao = table.Column<DateOnly>(type: "date", nullable: true),
                    FormaPagamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CartaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContaBancariaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ValorOriginal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorDesconto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorJuros = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorMulta = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorLiquido = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    QuantidadeParcelas = table.Column<int>(type: "int", nullable: false),
                    NumeroParcela = table.Column<int>(type: "int", nullable: false),
                    GrupoParcelamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    StatusContaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EhRecorrente = table.Column<bool>(type: "bit", nullable: false),
                    RegraRecorrenciaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Origem = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
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
                        name: "FK_contas_receber_status_conta_StatusContaId",
                        column: x => x.StatusContaId,
                        principalTable: "status_conta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "movimentacoes_financeiras",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataMovimentacao = table.Column<DateOnly>(type: "date", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Natureza = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ContaBancariaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContaPagarId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContaReceberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FaturaCartaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StatusMovimentacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DataConciliacao = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
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
                        name: "FK_movimentacoes_financeiras_status_movimentacao_StatusMovimentacaoId",
                        column: x => x.StatusMovimentacaoId,
                        principalTable: "status_movimentacao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rateios_conta_gerencial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoLancamento = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ContaPagarId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContaReceberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContaGerencialId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Percentual = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: true),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
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
                    { new Guid("55555555-5555-5555-5555-555555555555"), "PARCIAL", "Parcial" }
                });

            migrationBuilder.InsertData(
                table: "status_movimentacao",
                columns: new[] { "Id", "Codigo", "Nome" },
                values: new object[,]
                {
                    { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"), "PREVISTA", "Prevista" },
                    { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2"), "EFETIVADA", "Efetivada" },
                    { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3"), "CONCILIADA", "Conciliada" },
                    { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa4"), "CANCELADA", "Cancelada" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_CartaoId",
                table: "contas_pagar",
                column: "CartaoId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_ContaBancariaId",
                table: "contas_pagar",
                column: "ContaBancariaId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_DataVencimento",
                table: "contas_pagar",
                column: "DataVencimento");

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_FormaPagamentoId",
                table: "contas_pagar",
                column: "FormaPagamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_RecebedorId",
                table: "contas_pagar",
                column: "RecebedorId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_ResponsavelCompraId",
                table: "contas_pagar",
                column: "ResponsavelCompraId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_StatusContaId",
                table: "contas_pagar",
                column: "StatusContaId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_receber_CartaoId",
                table: "contas_receber",
                column: "CartaoId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_receber_ContaBancariaId",
                table: "contas_receber",
                column: "ContaBancariaId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_receber_DataVencimento",
                table: "contas_receber",
                column: "DataVencimento");

            migrationBuilder.CreateIndex(
                name: "IX_contas_receber_FormaPagamentoId",
                table: "contas_receber",
                column: "FormaPagamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_receber_PagadorId",
                table: "contas_receber",
                column: "PagadorId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_receber_ResponsavelId",
                table: "contas_receber",
                column: "ResponsavelId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_receber_StatusContaId",
                table: "contas_receber",
                column: "StatusContaId");

            migrationBuilder.CreateIndex(
                name: "IX_movimentacoes_financeiras_ContaBancariaId",
                table: "movimentacoes_financeiras",
                column: "ContaBancariaId");

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
                name: "IX_movimentacoes_financeiras_StatusMovimentacaoId",
                table: "movimentacoes_financeiras",
                column: "StatusMovimentacaoId");

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
                name: "IX_status_conta_Codigo",
                table: "status_conta",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_status_movimentacao_Codigo",
                table: "status_movimentacao",
                column: "Codigo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "movimentacoes_financeiras");

            migrationBuilder.DropTable(
                name: "rateios_conta_gerencial");

            migrationBuilder.DropTable(
                name: "status_movimentacao");

            migrationBuilder.DropTable(
                name: "contas_pagar");

            migrationBuilder.DropTable(
                name: "contas_receber");

            migrationBuilder.DropTable(
                name: "status_conta");
        }
    }
}
