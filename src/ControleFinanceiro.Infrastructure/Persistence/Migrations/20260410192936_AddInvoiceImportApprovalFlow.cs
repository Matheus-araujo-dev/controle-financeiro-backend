using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceImportApprovalFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_contas_pagar_CartaoId",
                table: "contas_pagar");

            migrationBuilder.AddColumn<string>(
                name: "ChaveSerieImportacaoCartao",
                table: "contas_pagar",
                type: "nvarchar(180)",
                maxLength: 180,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FaturaCartaoId",
                table: "contas_pagar",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrigemImportacaoWhatsappId",
                table: "contas_pagar",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_CartaoId_ChaveSerieImportacaoCartao_NumeroParcela_QuantidadeParcelas",
                table: "contas_pagar",
                columns: new[] { "CartaoId", "ChaveSerieImportacaoCartao", "NumeroParcela", "QuantidadeParcelas" },
                filter: "[CartaoId] IS NOT NULL AND [ChaveSerieImportacaoCartao] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_FaturaCartaoId",
                table: "contas_pagar",
                column: "FaturaCartaoId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_OrigemImportacaoWhatsappId",
                table: "contas_pagar",
                column: "OrigemImportacaoWhatsappId");

            migrationBuilder.AddForeignKey(
                name: "FK_contas_pagar_faturas_cartao_FaturaCartaoId",
                table: "contas_pagar",
                column: "FaturaCartaoId",
                principalTable: "faturas_cartao",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_contas_pagar_importacoes_whatsapp_OrigemImportacaoWhatsappId",
                table: "contas_pagar",
                column: "OrigemImportacaoWhatsappId",
                principalTable: "importacoes_whatsapp",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_contas_pagar_faturas_cartao_FaturaCartaoId",
                table: "contas_pagar");

            migrationBuilder.DropForeignKey(
                name: "FK_contas_pagar_importacoes_whatsapp_OrigemImportacaoWhatsappId",
                table: "contas_pagar");

            migrationBuilder.DropIndex(
                name: "IX_contas_pagar_CartaoId_ChaveSerieImportacaoCartao_NumeroParcela_QuantidadeParcelas",
                table: "contas_pagar");

            migrationBuilder.DropIndex(
                name: "IX_contas_pagar_FaturaCartaoId",
                table: "contas_pagar");

            migrationBuilder.DropIndex(
                name: "IX_contas_pagar_OrigemImportacaoWhatsappId",
                table: "contas_pagar");

            migrationBuilder.DropColumn(
                name: "ChaveSerieImportacaoCartao",
                table: "contas_pagar");

            migrationBuilder.DropColumn(
                name: "FaturaCartaoId",
                table: "contas_pagar");

            migrationBuilder.DropColumn(
                name: "OrigemImportacaoWhatsappId",
                table: "contas_pagar");

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_CartaoId",
                table: "contas_pagar",
                column: "CartaoId");
        }
    }
}
