using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarTabelasAgentaIA : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_conversas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Canal = table.Column<int>(type: "int", nullable: false),
                    ContatoExterno = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FamiliaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_conversas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ai_mensagens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Papel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Conteudo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_mensagens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_mensagens_ai_conversas_ConversaId",
                        column: x => x.ConversaId,
                        principalTable: "ai_conversas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_tool_calls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NomeFerramenta = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InputJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OutputJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TokensEntrada = table.Column<int>(type: "int", nullable: false),
                    TokensSaida = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_tool_calls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_tool_calls_ai_conversas_ConversaId",
                        column: x => x.ConversaId,
                        principalTable: "ai_conversas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_conversas_FamiliaId",
                table: "ai_conversas",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_conversas_UsuarioId",
                table: "ai_conversas",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_mensagens_ConversaId",
                table: "ai_mensagens",
                column: "ConversaId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_tool_calls_ConversaId",
                table: "ai_tool_calls",
                column: "ConversaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_mensagens");

            migrationBuilder.DropTable(
                name: "ai_tool_calls");

            migrationBuilder.DropTable(
                name: "ai_conversas");
        }
    }
}
