using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContaVinculadaEContaGerencialContraria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ContaVinculadaId",
                table: "contas_receber",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoContaVinculada",
                table: "contas_receber",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContaVinculadaId",
                table: "contas_pagar",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoContaVinculada",
                table: "contas_pagar",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContaGerencialContrariaId",
                table: "contas_gerenciais",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_contas_receber_ContaVinculadaId",
                table: "contas_receber",
                column: "ContaVinculadaId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_ContaVinculadaId",
                table: "contas_pagar",
                column: "ContaVinculadaId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_gerenciais_ContaGerencialContrariaId",
                table: "contas_gerenciais",
                column: "ContaGerencialContrariaId");

            migrationBuilder.AddForeignKey(
                name: "FK_contas_gerenciais_contas_gerenciais_ContaGerencialContraria~",
                table: "contas_gerenciais",
                column: "ContaGerencialContrariaId",
                principalTable: "contas_gerenciais",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_contas_gerenciais_contas_gerenciais_ContaGerencialContraria~",
                table: "contas_gerenciais");

            migrationBuilder.DropIndex(
                name: "IX_contas_receber_ContaVinculadaId",
                table: "contas_receber");

            migrationBuilder.DropIndex(
                name: "IX_contas_pagar_ContaVinculadaId",
                table: "contas_pagar");

            migrationBuilder.DropIndex(
                name: "IX_contas_gerenciais_ContaGerencialContrariaId",
                table: "contas_gerenciais");

            migrationBuilder.DropColumn(
                name: "ContaVinculadaId",
                table: "contas_receber");

            migrationBuilder.DropColumn(
                name: "TipoContaVinculada",
                table: "contas_receber");

            migrationBuilder.DropColumn(
                name: "ContaVinculadaId",
                table: "contas_pagar");

            migrationBuilder.DropColumn(
                name: "TipoContaVinculada",
                table: "contas_pagar");

            migrationBuilder.DropColumn(
                name: "ContaGerencialContrariaId",
                table: "contas_gerenciais");
        }
    }
}
