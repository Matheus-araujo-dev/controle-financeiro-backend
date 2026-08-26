using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCartaoRecebedorFormaPagamentoPadraoFatura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FormaPagamentoPadraoFaturaId",
                table: "cartoes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecebedorPadraoFaturaId",
                table: "cartoes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_cartoes_FormaPagamentoPadraoFaturaId",
                table: "cartoes",
                column: "FormaPagamentoPadraoFaturaId");

            migrationBuilder.CreateIndex(
                name: "IX_cartoes_RecebedorPadraoFaturaId",
                table: "cartoes",
                column: "RecebedorPadraoFaturaId");

            migrationBuilder.AddForeignKey(
                name: "FK_cartoes_formas_pagamento_FormaPagamentoPadraoFaturaId",
                table: "cartoes",
                column: "FormaPagamentoPadraoFaturaId",
                principalTable: "formas_pagamento",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_cartoes_pessoas_RecebedorPadraoFaturaId",
                table: "cartoes",
                column: "RecebedorPadraoFaturaId",
                principalTable: "pessoas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cartoes_formas_pagamento_FormaPagamentoPadraoFaturaId",
                table: "cartoes");

            migrationBuilder.DropForeignKey(
                name: "FK_cartoes_pessoas_RecebedorPadraoFaturaId",
                table: "cartoes");

            migrationBuilder.DropIndex(
                name: "IX_cartoes_FormaPagamentoPadraoFaturaId",
                table: "cartoes");

            migrationBuilder.DropIndex(
                name: "IX_cartoes_RecebedorPadraoFaturaId",
                table: "cartoes");

            migrationBuilder.DropColumn(
                name: "FormaPagamentoPadraoFaturaId",
                table: "cartoes");

            migrationBuilder.DropColumn(
                name: "RecebedorPadraoFaturaId",
                table: "cartoes");
        }
    }
}
