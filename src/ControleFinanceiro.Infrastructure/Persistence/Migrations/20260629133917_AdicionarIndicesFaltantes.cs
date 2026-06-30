using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarIndicesFaltantes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_contas_receber_PagadorId",
                table: "contas_receber");

            migrationBuilder.DropIndex(
                name: "IX_contas_receber_StatusContaId",
                table: "contas_receber");

            migrationBuilder.CreateIndex(
                name: "IX_pessoas_Ativo_Nome",
                table: "pessoas",
                columns: new[] { "Ativo", "Nome" });

            migrationBuilder.CreateIndex(
                name: "IX_pessoas_Nome",
                table: "pessoas",
                column: "Nome");

            migrationBuilder.CreateIndex(
                name: "IX_movimentacoes_financeiras_Natureza_StatusMovimentacaoId_DataMovimentacao",
                table: "movimentacoes_financeiras",
                columns: new[] { "Natureza", "StatusMovimentacaoId", "DataMovimentacao" });

            migrationBuilder.CreateIndex(
                name: "IX_contas_receber_DataEmissao",
                table: "contas_receber",
                column: "DataEmissao");

            migrationBuilder.CreateIndex(
                name: "IX_contas_receber_Descricao",
                table: "contas_receber",
                column: "Descricao");

            migrationBuilder.CreateIndex(
                name: "IX_contas_receber_PagadorId_StatusContaId",
                table: "contas_receber",
                columns: new[] { "PagadorId", "StatusContaId" });

            migrationBuilder.CreateIndex(
                name: "IX_contas_receber_StatusContaId_DataVencimento",
                table: "contas_receber",
                columns: new[] { "StatusContaId", "DataVencimento" });

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_DataEmissao",
                table: "contas_pagar",
                column: "DataEmissao");

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_GrupoParcelamentoId",
                table: "contas_pagar",
                column: "GrupoParcelamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_gerenciais_Codigo",
                table: "contas_gerenciais",
                column: "Codigo",
                unique: true,
                filter: "[Codigo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_contas_gerenciais_Tipo_Ativo",
                table: "contas_gerenciais",
                columns: new[] { "Tipo", "Ativo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_pessoas_Ativo_Nome",
                table: "pessoas");

            migrationBuilder.DropIndex(
                name: "IX_pessoas_Nome",
                table: "pessoas");

            migrationBuilder.DropIndex(
                name: "IX_movimentacoes_financeiras_Natureza_StatusMovimentacaoId_DataMovimentacao",
                table: "movimentacoes_financeiras");

            migrationBuilder.DropIndex(
                name: "IX_contas_receber_DataEmissao",
                table: "contas_receber");

            migrationBuilder.DropIndex(
                name: "IX_contas_receber_Descricao",
                table: "contas_receber");

            migrationBuilder.DropIndex(
                name: "IX_contas_receber_PagadorId_StatusContaId",
                table: "contas_receber");

            migrationBuilder.DropIndex(
                name: "IX_contas_receber_StatusContaId_DataVencimento",
                table: "contas_receber");

            migrationBuilder.DropIndex(
                name: "IX_contas_pagar_DataEmissao",
                table: "contas_pagar");

            migrationBuilder.DropIndex(
                name: "IX_contas_pagar_GrupoParcelamentoId",
                table: "contas_pagar");

            migrationBuilder.DropIndex(
                name: "IX_contas_gerenciais_Codigo",
                table: "contas_gerenciais");

            migrationBuilder.DropIndex(
                name: "IX_contas_gerenciais_Tipo_Ativo",
                table: "contas_gerenciais");

            migrationBuilder.CreateIndex(
                name: "IX_contas_receber_PagadorId",
                table: "contas_receber",
                column: "PagadorId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_receber_StatusContaId",
                table: "contas_receber",
                column: "StatusContaId");
        }
    }
}
