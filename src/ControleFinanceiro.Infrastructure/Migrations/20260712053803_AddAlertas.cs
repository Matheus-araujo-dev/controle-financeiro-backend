using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlertasDigitaisEnviados",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Canal = table.Column<string>(type: "text", nullable: false),
                    TipoAlerta = table.Column<string>(type: "text", nullable: false),
                    ChaveReferencia = table.Column<string>(type: "text", nullable: false),
                    DataEnvio = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertasDigitaisEnviados", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracoesNotificacao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailAtivo = table.Column<bool>(type: "boolean", nullable: false),
                    EmailDestinatario = table.Column<string>(type: "text", nullable: true),
                    EmailVencimento = table.Column<bool>(type: "boolean", nullable: false),
                    EmailDiasAntecedencia = table.Column<int>(type: "integer", nullable: false),
                    EmailLimiteCategoria = table.Column<bool>(type: "boolean", nullable: false),
                    PushAtivo = table.Column<bool>(type: "boolean", nullable: false),
                    PushVencimento = table.Column<bool>(type: "boolean", nullable: false),
                    PushDiasAntecedencia = table.Column<int>(type: "integer", nullable: false),
                    PushLimiteCategoria = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    FamiliaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracoesNotificacao", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PushSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Endpoint = table.Column<string>(type: "text", nullable: false),
                    P256dh = table.Column<string>(type: "text", nullable: false),
                    Auth = table.Column<string>(type: "text", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    FamiliaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracoesNotificacao_FamiliaId",
                table: "ConfiguracoesNotificacao",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_FamiliaId",
                table: "PushSubscriptions",
                column: "FamiliaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertasDigitaisEnviados");

            migrationBuilder.DropTable(
                name: "ConfiguracoesNotificacao");

            migrationBuilder.DropTable(
                name: "PushSubscriptions");
        }
    }
}
