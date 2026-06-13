using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarTabelasWhatsappFase2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalMessageId",
                table: "ai_mensagens",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "whatsapp_config_alertas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceberVencimento = table.Column<bool>(type: "bit", nullable: false),
                    DiasAntecedenciaVencimento = table.Column<int>(type: "int", nullable: false),
                    ReceberLimiteCategoria = table.Column<bool>(type: "bit", nullable: false),
                    ReceberLimiteResponsavel = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FamiliaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_config_alertas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_whatsapp_config_alertas_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "whatsapp_usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Telefone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    VerificadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FamiliaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_usuarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_whatsapp_usuarios_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_mensagens_ExternalMessageId",
                table: "ai_mensagens",
                column: "ExternalMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_config_alertas_FamiliaId",
                table: "whatsapp_config_alertas",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_config_alertas_UsuarioId",
                table: "whatsapp_config_alertas",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_usuarios_FamiliaId",
                table: "whatsapp_usuarios",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_usuarios_Telefone",
                table: "whatsapp_usuarios",
                column: "Telefone");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_usuarios_UsuarioId",
                table: "whatsapp_usuarios",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "whatsapp_config_alertas");

            migrationBuilder.DropTable(
                name: "whatsapp_usuarios");

            migrationBuilder.DropIndex(
                name: "IX_ai_mensagens_ExternalMessageId",
                table: "ai_mensagens");

            migrationBuilder.DropColumn(
                name: "ExternalMessageId",
                table: "ai_mensagens");
        }
    }
}
