using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarMetaOrcamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "metas_orcamento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContaGerencialId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Competencia = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    ValorMeta = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FamiliaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_metas_orcamento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_metas_orcamento_contas_gerenciais_ContaGerencialId",
                        column: x => x.ContaGerencialId,
                        principalTable: "contas_gerenciais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_metas_orcamento_ContaGerencialId",
                table: "metas_orcamento",
                column: "ContaGerencialId");

            migrationBuilder.CreateIndex(
                name: "IX_metas_orcamento_FamiliaId",
                table: "metas_orcamento",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_metas_orcamento_FamiliaId_ContaGerencialId_Competencia",
                table: "metas_orcamento",
                columns: new[] { "FamiliaId", "ContaGerencialId", "Competencia" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "metas_orcamento");
        }
    }
}
