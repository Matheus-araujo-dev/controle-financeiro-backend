using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPessoaContasGerenciaisPadrao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ContaGerencialDespesaId",
                table: "pessoas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContaGerencialReceitaId",
                table: "pessoas",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_pessoas_ContaGerencialDespesaId",
                table: "pessoas",
                column: "ContaGerencialDespesaId");

            migrationBuilder.CreateIndex(
                name: "IX_pessoas_ContaGerencialReceitaId",
                table: "pessoas",
                column: "ContaGerencialReceitaId");

            migrationBuilder.AddForeignKey(
                name: "FK_pessoas_contas_gerenciais_ContaGerencialDespesaId",
                table: "pessoas",
                column: "ContaGerencialDespesaId",
                principalTable: "contas_gerenciais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_pessoas_contas_gerenciais_ContaGerencialReceitaId",
                table: "pessoas",
                column: "ContaGerencialReceitaId",
                principalTable: "contas_gerenciais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_pessoas_contas_gerenciais_ContaGerencialDespesaId",
                table: "pessoas");

            migrationBuilder.DropForeignKey(
                name: "FK_pessoas_contas_gerenciais_ContaGerencialReceitaId",
                table: "pessoas");

            migrationBuilder.DropIndex(
                name: "IX_pessoas_ContaGerencialDespesaId",
                table: "pessoas");

            migrationBuilder.DropIndex(
                name: "IX_pessoas_ContaGerencialReceitaId",
                table: "pessoas");

            migrationBuilder.DropColumn(
                name: "ContaGerencialDespesaId",
                table: "pessoas");

            migrationBuilder.DropColumn(
                name: "ContaGerencialReceitaId",
                table: "pessoas");
        }
    }
}
