using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanoContaPagarConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ContaGerencialId",
                table: "planos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FormaPagamentoId",
                table: "planos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecebedorId",
                table: "planos",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_planos_ContaGerencialId",
                table: "planos",
                column: "ContaGerencialId");

            migrationBuilder.CreateIndex(
                name: "IX_planos_FormaPagamentoId",
                table: "planos",
                column: "FormaPagamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_planos_RecebedorId",
                table: "planos",
                column: "RecebedorId");

            migrationBuilder.AddForeignKey(
                name: "FK_planos_contas_gerenciais_ContaGerencialId",
                table: "planos",
                column: "ContaGerencialId",
                principalTable: "contas_gerenciais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_planos_formas_pagamento_FormaPagamentoId",
                table: "planos",
                column: "FormaPagamentoId",
                principalTable: "formas_pagamento",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_planos_pessoas_RecebedorId",
                table: "planos",
                column: "RecebedorId",
                principalTable: "pessoas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_planos_contas_gerenciais_ContaGerencialId",
                table: "planos");

            migrationBuilder.DropForeignKey(
                name: "FK_planos_formas_pagamento_FormaPagamentoId",
                table: "planos");

            migrationBuilder.DropForeignKey(
                name: "FK_planos_pessoas_RecebedorId",
                table: "planos");

            migrationBuilder.DropIndex(
                name: "IX_planos_ContaGerencialId",
                table: "planos");

            migrationBuilder.DropIndex(
                name: "IX_planos_FormaPagamentoId",
                table: "planos");

            migrationBuilder.DropIndex(
                name: "IX_planos_RecebedorId",
                table: "planos");

            migrationBuilder.DropColumn(
                name: "ContaGerencialId",
                table: "planos");

            migrationBuilder.DropColumn(
                name: "FormaPagamentoId",
                table: "planos");

            migrationBuilder.DropColumn(
                name: "RecebedorId",
                table: "planos");
        }
    }
}
