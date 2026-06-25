using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarAnexosFinanceiros : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "anexos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NomeArquivoOriginal = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CaminhoArquivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MimeType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TamanhoBytes = table.Column<long>(type: "bigint", nullable: false),
                    HashSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Origem = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ConversaAiId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ImportacaoWhatsappId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FamiliaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anexos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "anexo_vinculos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnexoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoEntidade = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EntidadeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FamiliaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anexo_vinculos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_anexo_vinculos_anexos_AnexoId",
                        column: x => x.AnexoId,
                        principalTable: "anexos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_anexo_vinculos_AnexoId_TipoEntidade_EntidadeId",
                table: "anexo_vinculos",
                columns: new[] { "AnexoId", "TipoEntidade", "EntidadeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_anexo_vinculos_FamiliaId",
                table: "anexo_vinculos",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_anexo_vinculos_TipoEntidade_EntidadeId",
                table: "anexo_vinculos",
                columns: new[] { "TipoEntidade", "EntidadeId" });

            migrationBuilder.CreateIndex(
                name: "IX_anexos_ConversaAiId",
                table: "anexos",
                column: "ConversaAiId");

            migrationBuilder.CreateIndex(
                name: "IX_anexos_FamiliaId",
                table: "anexos",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_anexos_HashSha256",
                table: "anexos",
                column: "HashSha256");

            migrationBuilder.CreateIndex(
                name: "IX_anexos_ImportacaoWhatsappId",
                table: "anexos",
                column: "ImportacaoWhatsappId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "anexo_vinculos");

            migrationBuilder.DropTable(
                name: "anexos");
        }
    }
}
