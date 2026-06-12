using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PostMvpLimiteCompartilhadoEComprasPlanejadas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LimiteCartoesCompartilhado",
                table: "contas_bancarias",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "compras_planejadas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ValorEstimado = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DataDesejada = table.Column<DateOnly>(type: "date", nullable: true),
                    Prioridade = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Parcelavel = table.Column<bool>(type: "bit", nullable: false),
                    QuantidadeParcelasDesejada = table.Column<int>(type: "int", nullable: true),
                    ContaGerencialId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResponsavelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compras_planejadas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_compras_planejadas_contas_gerenciais_ContaGerencialId",
                        column: x => x.ContaGerencialId,
                        principalTable: "contas_gerenciais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_compras_planejadas_pessoas_ResponsavelId",
                        column: x => x.ResponsavelId,
                        principalTable: "pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_compras_planejadas_ContaGerencialId",
                table: "compras_planejadas",
                column: "ContaGerencialId");

            migrationBuilder.CreateIndex(
                name: "IX_compras_planejadas_ResponsavelId",
                table: "compras_planejadas",
                column: "ResponsavelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "compras_planejadas");

            migrationBuilder.DropColumn(
                name: "LimiteCartoesCompartilhado",
                table: "contas_bancarias");
        }
    }
}
