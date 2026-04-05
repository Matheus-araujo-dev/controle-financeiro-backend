using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase5Recorrencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "regras_recorrencia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoLancamento = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TipoPeriodicidade = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DiaGeracaoMensal = table.Column<int>(type: "int", nullable: false),
                    DataInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    DataFim = table.Column<DateOnly>(type: "date", nullable: true),
                    Ativa = table.Column<bool>(type: "bit", nullable: false),
                    PermiteEdicaoOcorrenciaIndividual = table.Column<bool>(type: "bit", nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TemplateJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regras_recorrencia", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contas_receber_RegraRecorrenciaId",
                table: "contas_receber",
                column: "RegraRecorrenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_RegraRecorrenciaId",
                table: "contas_pagar",
                column: "RegraRecorrenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_regras_recorrencia_TipoLancamento_Ativa",
                table: "regras_recorrencia",
                columns: new[] { "TipoLancamento", "Ativa" });

            migrationBuilder.AddForeignKey(
                name: "FK_contas_pagar_regras_recorrencia_RegraRecorrenciaId",
                table: "contas_pagar",
                column: "RegraRecorrenciaId",
                principalTable: "regras_recorrencia",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_contas_receber_regras_recorrencia_RegraRecorrenciaId",
                table: "contas_receber",
                column: "RegraRecorrenciaId",
                principalTable: "regras_recorrencia",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_contas_pagar_regras_recorrencia_RegraRecorrenciaId",
                table: "contas_pagar");

            migrationBuilder.DropForeignKey(
                name: "FK_contas_receber_regras_recorrencia_RegraRecorrenciaId",
                table: "contas_receber");

            migrationBuilder.DropTable(
                name: "regras_recorrencia");

            migrationBuilder.DropIndex(
                name: "IX_contas_receber_RegraRecorrenciaId",
                table: "contas_receber");

            migrationBuilder.DropIndex(
                name: "IX_contas_pagar_RegraRecorrenciaId",
                table: "contas_pagar");
        }
    }
}
