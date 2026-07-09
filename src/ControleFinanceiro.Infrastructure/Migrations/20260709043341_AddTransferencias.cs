using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTransferencias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TransferenciaId",
                table: "movimentacoes_financeiras",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Transferencias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContaBancariaOrigemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContaBancariaDestinoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric", nullable: false),
                    DataTransferencia = table.Column<DateOnly>(type: "date", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: true),
                    Cancelada = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    FamiliaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transferencias", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_movimentacoes_financeiras_TransferenciaId",
                table: "movimentacoes_financeiras",
                column: "TransferenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_Transferencias_FamiliaId",
                table: "Transferencias",
                column: "FamiliaId");

            migrationBuilder.AddForeignKey(
                name: "FK_movimentacoes_financeiras_Transferencias_TransferenciaId",
                table: "movimentacoes_financeiras",
                column: "TransferenciaId",
                principalTable: "Transferencias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_movimentacoes_financeiras_Transferencias_TransferenciaId",
                table: "movimentacoes_financeiras");

            migrationBuilder.DropTable(
                name: "Transferencias");

            migrationBuilder.DropIndex(
                name: "IX_movimentacoes_financeiras_TransferenciaId",
                table: "movimentacoes_financeiras");

            migrationBuilder.DropColumn(
                name: "TransferenciaId",
                table: "movimentacoes_financeiras");
        }
    }
}
