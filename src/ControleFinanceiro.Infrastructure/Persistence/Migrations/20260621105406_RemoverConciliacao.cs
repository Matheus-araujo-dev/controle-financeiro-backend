using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoverConciliacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_itens_importados_whatsapp_movimentacoes_financeiras_MovimentacaoFinanceiraId",
                table: "itens_importados_whatsapp");

            migrationBuilder.DropIndex(
                name: "IX_itens_importados_whatsapp_MovimentacaoFinanceiraId",
                table: "itens_importados_whatsapp");

            migrationBuilder.Sql("""
                UPDATE [movimentacoes_financeiras]
                SET [StatusMovimentacaoId] = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2'
                WHERE [StatusMovimentacaoId] = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3'
                """);

            migrationBuilder.DeleteData(
                table: "status_movimentacao",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3"));

            migrationBuilder.DropColumn(
                name: "DataConciliacao",
                table: "movimentacoes_financeiras");

            migrationBuilder.DropColumn(
                name: "MovimentacaoFinanceiraId",
                table: "itens_importados_whatsapp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DataConciliacao",
                table: "movimentacoes_financeiras",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MovimentacaoFinanceiraId",
                table: "itens_importados_whatsapp",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.InsertData(
                table: "status_movimentacao",
                columns: new[] { "Id", "Codigo", "Nome" },
                values: new object[] { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3"), "CONCILIADA", "Conciliada" });

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
    }
}
