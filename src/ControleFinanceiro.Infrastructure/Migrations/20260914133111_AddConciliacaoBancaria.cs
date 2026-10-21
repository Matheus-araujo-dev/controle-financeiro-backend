using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConciliacaoBancaria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "conciliacoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NomeArquivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Formato = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ContaBancariaId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    DataFim = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalItens = table.Column<int>(type: "integer", nullable: false),
                    ItensConciliados = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    FamiliaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conciliacoes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "itens_conciliacao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConciliacaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    Descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Valor = table.Column<decimal>(type: "numeric", nullable: false),
                    Documento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StatusItem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MovimentacaoVinculadaId = table.Column<Guid>(type: "uuid", nullable: true),
                    SugestaoMovimentacaoId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScoreSugestao = table.Column<decimal>(type: "numeric", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    FamiliaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_itens_conciliacao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_itens_conciliacao_conciliacoes_ConciliacaoId",
                        column: x => x.ConciliacaoId,
                        principalTable: "conciliacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_conciliacoes_ContaBancariaId",
                table: "conciliacoes",
                column: "ContaBancariaId");

            migrationBuilder.CreateIndex(
                name: "IX_conciliacoes_FamiliaId",
                table: "conciliacoes",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_itens_conciliacao_ConciliacaoId",
                table: "itens_conciliacao",
                column: "ConciliacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_itens_conciliacao_FamiliaId",
                table: "itens_conciliacao",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_itens_conciliacao_MovimentacaoVinculadaId",
                table: "itens_conciliacao",
                column: "MovimentacaoVinculadaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "itens_conciliacao");

            migrationBuilder.DropTable(
                name: "conciliacoes");
        }
    }
}
