using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConciliacaoFatura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChaveOrigem",
                table: "itens_conciliacao",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContaPagarVinculadaId",
                table: "itens_conciliacao",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumeroParcela",
                table: "itens_conciliacao",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "QuantidadeParcelas",
                table: "itens_conciliacao",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "RascunhoJson",
                table: "itens_conciliacao",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorAnteriorSistema",
                table: "itens_conciliacao",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ContaBancariaId",
                table: "conciliacoes",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "FaturaId",
                table: "conciliacoes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HashArquivo",
                table: "conciliacoes",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_itens_conciliacao_ConciliacaoId_ChaveOrigem",
                table: "itens_conciliacao",
                columns: new[] { "ConciliacaoId", "ChaveOrigem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_itens_conciliacao_ConciliacaoId_ContaPagarVinculadaId",
                table: "itens_conciliacao",
                columns: new[] { "ConciliacaoId", "ContaPagarVinculadaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_itens_conciliacao_ContaPagarVinculadaId",
                table: "itens_conciliacao",
                column: "ContaPagarVinculadaId");

            migrationBuilder.CreateIndex(
                name: "IX_conciliacoes_FamiliaId_FaturaId_HashArquivo",
                table: "conciliacoes",
                columns: new[] { "FamiliaId", "FaturaId", "HashArquivo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_conciliacoes_FaturaId",
                table: "conciliacoes",
                column: "FaturaId");

            migrationBuilder.AddForeignKey(
                name: "FK_conciliacoes_faturas_cartao_FaturaId",
                table: "conciliacoes",
                column: "FaturaId",
                principalTable: "faturas_cartao",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_itens_conciliacao_contas_pagar_ContaPagarVinculadaId",
                table: "itens_conciliacao",
                column: "ContaPagarVinculadaId",
                principalTable: "contas_pagar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_conciliacoes_faturas_cartao_FaturaId",
                table: "conciliacoes");

            migrationBuilder.DropForeignKey(
                name: "FK_itens_conciliacao_contas_pagar_ContaPagarVinculadaId",
                table: "itens_conciliacao");

            migrationBuilder.DropIndex(
                name: "IX_itens_conciliacao_ConciliacaoId_ChaveOrigem",
                table: "itens_conciliacao");

            migrationBuilder.DropIndex(
                name: "IX_itens_conciliacao_ConciliacaoId_ContaPagarVinculadaId",
                table: "itens_conciliacao");

            migrationBuilder.DropIndex(
                name: "IX_itens_conciliacao_ContaPagarVinculadaId",
                table: "itens_conciliacao");

            migrationBuilder.DropIndex(
                name: "IX_conciliacoes_FamiliaId_FaturaId_HashArquivo",
                table: "conciliacoes");

            migrationBuilder.DropIndex(
                name: "IX_conciliacoes_FaturaId",
                table: "conciliacoes");

            migrationBuilder.DropColumn(
                name: "ChaveOrigem",
                table: "itens_conciliacao");

            migrationBuilder.DropColumn(
                name: "ContaPagarVinculadaId",
                table: "itens_conciliacao");

            migrationBuilder.DropColumn(
                name: "NumeroParcela",
                table: "itens_conciliacao");

            migrationBuilder.DropColumn(
                name: "QuantidadeParcelas",
                table: "itens_conciliacao");

            migrationBuilder.DropColumn(
                name: "RascunhoJson",
                table: "itens_conciliacao");

            migrationBuilder.DropColumn(
                name: "ValorAnteriorSistema",
                table: "itens_conciliacao");

            migrationBuilder.DropColumn(
                name: "FaturaId",
                table: "conciliacoes");

            migrationBuilder.DropColumn(
                name: "HashArquivo",
                table: "conciliacoes");

            migrationBuilder.AlterColumn<Guid>(
                name: "ContaBancariaId",
                table: "conciliacoes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
