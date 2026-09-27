using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFechamentoMensal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fechamentos_mensais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Competencia = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FechadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechadoEmUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TotalReceitasSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalDespesasSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SaldoSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalPendenteSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalVencidoSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    QuantidadeLancamentosSnapshot = table.Column<int>(type: "integer", nullable: false),
                    QuantidadeBloqueiosSnapshot = table.Column<int>(type: "integer", nullable: false),
                    ReabertoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReabertoEmUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    JustificativaReabertura = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    FamiliaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fechamentos_mensais", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fechamentos_mensais_FamiliaId",
                table: "fechamentos_mensais",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_fechamentos_mensais_FamiliaId_Competencia",
                table: "fechamentos_mensais",
                columns: new[] { "FamiliaId", "Competencia" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fechamentos_mensais");
        }
    }
}
