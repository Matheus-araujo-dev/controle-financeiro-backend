using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexCartaoStatusConta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_CartaoId_StatusContaId",
                table: "contas_pagar",
                columns: new[] { "CartaoId", "StatusContaId" },
                filter: "\"CartaoId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_contas_pagar_CartaoId_StatusContaId",
                table: "contas_pagar");
        }
    }
}
