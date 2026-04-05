using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase7ImportacoesWhatsapp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "importacoes_whatsapp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoOrigem = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Remetente = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    TextoBruto = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NomeArquivo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CaminhoArquivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MimeType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ConfiancaExtracao = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true),
                    MensagemErro = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RecebidoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejeitadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_importacoes_whatsapp", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "itens_importados_whatsapp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImportacaoWhatsappId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoSugestao = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PayloadSugeridoJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ConfirmadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejeitadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_itens_importados_whatsapp", x => x.Id);
                    table.ForeignKey(
                        name: "FK_itens_importados_whatsapp_importacoes_whatsapp_ImportacaoWhatsappId",
                        column: x => x.ImportacaoWhatsappId,
                        principalTable: "importacoes_whatsapp",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_importacoes_whatsapp_Status",
                table: "importacoes_whatsapp",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_itens_importados_whatsapp_ImportacaoWhatsappId",
                table: "itens_importados_whatsapp",
                column: "ImportacaoWhatsappId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "itens_importados_whatsapp");

            migrationBuilder.DropTable(
                name: "importacoes_whatsapp");
        }
    }
}
