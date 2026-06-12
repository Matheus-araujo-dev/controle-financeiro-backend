using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContaGerencialResponsavelPadrao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ResponsavelPadraoId",
                table: "contas_gerenciais",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_contas_gerenciais_ResponsavelPadraoId",
                table: "contas_gerenciais",
                column: "ResponsavelPadraoId");

            migrationBuilder.AddForeignKey(
                name: "FK_contas_gerenciais_pessoas_ResponsavelPadraoId",
                table: "contas_gerenciais",
                column: "ResponsavelPadraoId",
                principalTable: "pessoas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_contas_gerenciais_pessoas_ResponsavelPadraoId",
                table: "contas_gerenciais");

            migrationBuilder.DropIndex(
                name: "IX_contas_gerenciais_ResponsavelPadraoId",
                table: "contas_gerenciais");

            migrationBuilder.DropColumn(
                name: "ResponsavelPadraoId",
                table: "contas_gerenciais");
        }
    }
}
