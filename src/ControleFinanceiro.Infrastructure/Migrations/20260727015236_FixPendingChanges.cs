using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixPendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_contas_receber_GrupoResponsaveisId",
                table: "contas_receber");

            migrationBuilder.CreateIndex(
                name: "IX_contas_receber_GrupoResponsaveisId",
                table: "contas_receber",
                column: "GrupoResponsaveisId",
                filter: "\"GrupoResponsaveisId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_contas_receber_GrupoResponsaveisId",
                table: "contas_receber");

            migrationBuilder.CreateIndex(
                name: "IX_contas_receber_GrupoResponsaveisId",
                table: "contas_receber",
                column: "GrupoResponsaveisId",
                filter: "\"GrupoReembolsoId\" IS NOT NULL");
        }
    }
}
