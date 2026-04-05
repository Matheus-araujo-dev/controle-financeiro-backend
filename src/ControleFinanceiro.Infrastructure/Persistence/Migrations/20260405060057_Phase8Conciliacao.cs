using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase8Conciliacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MovimentacaoFinanceiraId",
                table: "itens_importados_whatsapp",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_itens_importados_whatsapp_MovimentacaoFinanceiraId",
                table: "itens_importados_whatsapp",
                column: "MovimentacaoFinanceiraId");

            migrationBuilder.AddForeignKey(
                name: "FK_itens_importados_whatsapp_movimentacoes_financeiras_MovimentacaoFinanceiraId",
                table: "itens_importados_whatsapp",
                column: "MovimentacaoFinanceiraId",
                principalTable: "movimentacoes_financeiras",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_itens_importados_whatsapp_movimentacoes_financeiras_MovimentacaoFinanceiraId",
                table: "itens_importados_whatsapp");

            migrationBuilder.DropIndex(
                name: "IX_itens_importados_whatsapp_MovimentacaoFinanceiraId",
                table: "itens_importados_whatsapp");

            migrationBuilder.DropColumn(
                name: "MovimentacaoFinanceiraId",
                table: "itens_importados_whatsapp");
        }
    }
}
