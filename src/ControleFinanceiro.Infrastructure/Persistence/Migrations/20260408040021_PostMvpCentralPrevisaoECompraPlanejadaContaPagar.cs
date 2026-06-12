using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PostMvpCentralPrevisaoECompraPlanejadaContaPagar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrigemCompraPlanejadaId",
                table: "contas_pagar",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContaPagarGeradaId",
                table: "compras_planejadas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConvertidaEmContaPagarEmUtc",
                table: "compras_planejadas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_OrigemCompraPlanejadaId",
                table: "contas_pagar",
                column: "OrigemCompraPlanejadaId");

            migrationBuilder.CreateIndex(
                name: "IX_compras_planejadas_ContaPagarGeradaId",
                table: "compras_planejadas",
                column: "ContaPagarGeradaId");

            migrationBuilder.AddForeignKey(
                name: "FK_compras_planejadas_contas_pagar_ContaPagarGeradaId",
                table: "compras_planejadas",
                column: "ContaPagarGeradaId",
                principalTable: "contas_pagar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_contas_pagar_compras_planejadas_OrigemCompraPlanejadaId",
                table: "contas_pagar",
                column: "OrigemCompraPlanejadaId",
                principalTable: "compras_planejadas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_compras_planejadas_contas_pagar_ContaPagarGeradaId",
                table: "compras_planejadas");

            migrationBuilder.DropForeignKey(
                name: "FK_contas_pagar_compras_planejadas_OrigemCompraPlanejadaId",
                table: "contas_pagar");

            migrationBuilder.DropIndex(
                name: "IX_contas_pagar_OrigemCompraPlanejadaId",
                table: "contas_pagar");

            migrationBuilder.DropIndex(
                name: "IX_compras_planejadas_ContaPagarGeradaId",
                table: "compras_planejadas");

            migrationBuilder.DropColumn(
                name: "OrigemCompraPlanejadaId",
                table: "contas_pagar");

            migrationBuilder.DropColumn(
                name: "ContaPagarGeradaId",
                table: "compras_planejadas");

            migrationBuilder.DropColumn(
                name: "ConvertidaEmContaPagarEmUtc",
                table: "compras_planejadas");
        }
    }
}
