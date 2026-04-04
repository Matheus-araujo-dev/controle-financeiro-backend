using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase4CartoesEFaturas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "faturas_cartao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CartaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Competencia = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    DataFechamento = table.Column<DateOnly>(type: "date", nullable: false),
                    DataVencimento = table.Column<DateOnly>(type: "date", nullable: false),
                    ValorTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DataPagamento = table.Column<DateOnly>(type: "date", nullable: true),
                    ContaBancariaPagamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
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

            migrationBuilder.CreateIndex(
                name: "IX_movimentacoes_financeiras_FaturaCartaoId",
                table: "movimentacoes_financeiras",
                column: "FaturaCartaoId");

            migrationBuilder.CreateIndex(
                name: "IX_faturas_cartao_CartaoId_Competencia",
                table: "faturas_cartao",
                columns: new[] { "CartaoId", "Competencia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_faturas_cartao_ContaBancariaPagamentoId",
                table: "faturas_cartao",
                column: "ContaBancariaPagamentoId");

            migrationBuilder.AddForeignKey(
                name: "FK_movimentacoes_financeiras_faturas_cartao_FaturaCartaoId",
                table: "movimentacoes_financeiras",
                column: "FaturaCartaoId",
                principalTable: "faturas_cartao",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_movimentacoes_financeiras_faturas_cartao_FaturaCartaoId",
                table: "movimentacoes_financeiras");

            migrationBuilder.DropTable(
                name: "faturas_cartao");

            migrationBuilder.DropIndex(
                name: "IX_movimentacoes_financeiras_FaturaCartaoId",
                table: "movimentacoes_financeiras");
        }
    }
}
