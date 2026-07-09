using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvestimentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "investimentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Emissor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Liquidez = table.Column<int>(type: "integer", nullable: false),
                    ValorInvestido = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorAtual = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DataAplicacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataVencimento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TaxaAnual = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: true),
                    ContaBancariaVinculadaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Encerrado = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    FamiliaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investimentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_investimentos_contas_bancarias_ContaBancariaVinculadaId",
                        column: x => x.ContaBancariaVinculadaId,
                        principalTable: "contas_bancarias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_investimentos_ContaBancariaVinculadaId",
                table: "investimentos",
                column: "ContaBancariaVinculadaId");

            migrationBuilder.CreateIndex(
                name: "IX_investimentos_FamiliaId",
                table: "investimentos",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_investimentos_FamiliaId_Tipo",
                table: "investimentos",
                columns: new[] { "FamiliaId", "Tipo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "investimentos");
        }
    }
}
