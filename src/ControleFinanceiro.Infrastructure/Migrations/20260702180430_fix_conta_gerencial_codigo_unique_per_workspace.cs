using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class fix_conta_gerencial_codigo_unique_per_workspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_contas_gerenciais_Codigo",
                table: "contas_gerenciais");

            migrationBuilder.CreateIndex(
                name: "IX_contas_gerenciais_FamiliaId_Codigo",
                table: "contas_gerenciais",
                columns: new[] { "FamiliaId", "Codigo" },
                unique: true,
                filter: "\"Codigo\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_contas_gerenciais_FamiliaId_Codigo",
                table: "contas_gerenciais");

            migrationBuilder.CreateIndex(
                name: "IX_contas_gerenciais_Codigo",
                table: "contas_gerenciais",
                column: "Codigo",
                unique: true,
                filter: "\"Codigo\" IS NOT NULL");
        }
    }
}
