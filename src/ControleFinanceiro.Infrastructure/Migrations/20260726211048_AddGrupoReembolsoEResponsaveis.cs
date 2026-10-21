using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGrupoReembolsoEResponsaveis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GrupoReembolsoId",
                table: "contas_receber",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GrupoResponsaveisId",
                table: "contas_receber",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GrupoReembolsoId",
                table: "contas_pagar",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GrupoResponsaveisId",
                table: "contas_pagar",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_contas_receber_GrupoReembolsoId",
                table: "contas_receber",
                column: "GrupoReembolsoId",
                filter: "\"GrupoReembolsoId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_contas_receber_GrupoResponsaveisId",
                table: "contas_receber",
                column: "GrupoResponsaveisId",
                filter: "\"GrupoResponsaveisId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_GrupoReembolsoId",
                table: "contas_pagar",
                column: "GrupoReembolsoId",
                filter: "\"GrupoReembolsoId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_GrupoResponsaveisId",
                table: "contas_pagar",
                column: "GrupoResponsaveisId",
                filter: "\"GrupoResponsaveisId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_contas_receber_GrupoReembolsoId",
                table: "contas_receber");

            migrationBuilder.DropIndex(
                name: "IX_contas_receber_GrupoResponsaveisId",
                table: "contas_receber");

            migrationBuilder.DropIndex(
                name: "IX_contas_pagar_GrupoReembolsoId",
                table: "contas_pagar");

            migrationBuilder.DropIndex(
                name: "IX_contas_pagar_GrupoResponsaveisId",
                table: "contas_pagar");

            migrationBuilder.DropColumn(
                name: "GrupoReembolsoId",
                table: "contas_receber");

            migrationBuilder.DropColumn(
                name: "GrupoResponsaveisId",
                table: "contas_receber");

            migrationBuilder.DropColumn(
                name: "GrupoReembolsoId",
                table: "contas_pagar");

            migrationBuilder.DropColumn(
                name: "GrupoResponsaveisId",
                table: "contas_pagar");
        }
    }
}
