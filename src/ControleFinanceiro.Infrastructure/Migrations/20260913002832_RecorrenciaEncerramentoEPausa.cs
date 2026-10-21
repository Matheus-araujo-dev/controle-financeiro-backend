using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RecorrenciaEncerramentoEPausa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Encerrada",
                table: "regras_recorrencia",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "GerarAPartirDe",
                table: "regras_recorrencia",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanceladaPorPausaRecorrencia",
                table: "contas_receber",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanceladaPorPausaRecorrencia",
                table: "contas_pagar",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Encerrada",
                table: "regras_recorrencia");

            migrationBuilder.DropColumn(
                name: "GerarAPartirDe",
                table: "regras_recorrencia");

            migrationBuilder.DropColumn(
                name: "CanceladaPorPausaRecorrencia",
                table: "contas_receber");

            migrationBuilder.DropColumn(
                name: "CanceladaPorPausaRecorrencia",
                table: "contas_pagar");
        }
    }
}
