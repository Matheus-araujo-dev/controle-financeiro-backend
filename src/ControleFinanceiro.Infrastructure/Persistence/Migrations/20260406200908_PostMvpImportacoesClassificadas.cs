using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PostMvpImportacoesClassificadas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChaveAprendizado",
                table: "itens_importados_whatsapp",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContaGerencialId",
                table: "itens_importados_whatsapp",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContaReceberId",
                table: "itens_importados_whatsapp",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResponsavelId",
                table: "itens_importados_whatsapp",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EhPadraoRecebimentoFaturaCartao",
                table: "contas_gerenciais",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                IF NOT EXISTS (
                    SELECT 1
                    FROM contas_gerenciais
                    WHERE Tipo = N'Receita'
                      AND Descricao = N'Recebimento de divida'
                )
                BEGIN
                    INSERT INTO contas_gerenciais (
                        Id,
                        Codigo,
                        Descricao,
                        Tipo,
                        ContaPaiId,
                        Ativo,
                        EhPadraoRecebimentoFaturaCartao,
                        CreatedAtUtc,
                        UpdatedAtUtc,
                        CreatedBy,
                        UpdatedBy)
                    VALUES (
                        NEWID(),
                        N'REC.DIVIDA',
                        N'Recebimento de divida',
                        N'Receita',
                        NULL,
                        1,
                        CASE WHEN EXISTS (SELECT 1 FROM contas_gerenciais WHERE EhPadraoRecebimentoFaturaCartao = 1) THEN 0 ELSE 1 END,
                        SYSUTCDATETIME(),
                        SYSUTCDATETIME(),
                        N'system',
                        N'system');
                END
                ELSE IF NOT EXISTS (
                    SELECT 1
                    FROM contas_gerenciais
                    WHERE EhPadraoRecebimentoFaturaCartao = 1
                )
                BEGIN
                    UPDATE contas_gerenciais
                    SET
                        EhPadraoRecebimentoFaturaCartao = 1,
                        UpdatedAtUtc = SYSUTCDATETIME(),
                        UpdatedBy = N'system'
                    WHERE Tipo = N'Receita'
                      AND Descricao = N'Recebimento de divida';
                END
                """);

            migrationBuilder.CreateIndex(
                name: "IX_itens_importados_whatsapp_ChaveAprendizado",
                table: "itens_importados_whatsapp",
                column: "ChaveAprendizado");

            migrationBuilder.CreateIndex(
                name: "IX_itens_importados_whatsapp_ContaGerencialId",
                table: "itens_importados_whatsapp",
                column: "ContaGerencialId");

            migrationBuilder.CreateIndex(
                name: "IX_itens_importados_whatsapp_ContaReceberId",
                table: "itens_importados_whatsapp",
                column: "ContaReceberId");

            migrationBuilder.CreateIndex(
                name: "IX_itens_importados_whatsapp_ResponsavelId",
                table: "itens_importados_whatsapp",
                column: "ResponsavelId");

            migrationBuilder.AddForeignKey(
                name: "FK_itens_importados_whatsapp_contas_gerenciais_ContaGerencialId",
                table: "itens_importados_whatsapp",
                column: "ContaGerencialId",
                principalTable: "contas_gerenciais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_itens_importados_whatsapp_contas_receber_ContaReceberId",
                table: "itens_importados_whatsapp",
                column: "ContaReceberId",
                principalTable: "contas_receber",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_itens_importados_whatsapp_pessoas_ResponsavelId",
                table: "itens_importados_whatsapp",
                column: "ResponsavelId",
                principalTable: "pessoas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_itens_importados_whatsapp_contas_gerenciais_ContaGerencialId",
                table: "itens_importados_whatsapp");

            migrationBuilder.DropForeignKey(
                name: "FK_itens_importados_whatsapp_contas_receber_ContaReceberId",
                table: "itens_importados_whatsapp");

            migrationBuilder.DropForeignKey(
                name: "FK_itens_importados_whatsapp_pessoas_ResponsavelId",
                table: "itens_importados_whatsapp");

            migrationBuilder.DropIndex(
                name: "IX_itens_importados_whatsapp_ChaveAprendizado",
                table: "itens_importados_whatsapp");

            migrationBuilder.DropIndex(
                name: "IX_itens_importados_whatsapp_ContaGerencialId",
                table: "itens_importados_whatsapp");

            migrationBuilder.DropIndex(
                name: "IX_itens_importados_whatsapp_ContaReceberId",
                table: "itens_importados_whatsapp");

            migrationBuilder.DropIndex(
                name: "IX_itens_importados_whatsapp_ResponsavelId",
                table: "itens_importados_whatsapp");

            migrationBuilder.DropColumn(
                name: "ChaveAprendizado",
                table: "itens_importados_whatsapp");

            migrationBuilder.DropColumn(
                name: "ContaGerencialId",
                table: "itens_importados_whatsapp");

            migrationBuilder.DropColumn(
                name: "ContaReceberId",
                table: "itens_importados_whatsapp");

            migrationBuilder.DropColumn(
                name: "ResponsavelId",
                table: "itens_importados_whatsapp");

            migrationBuilder.DropColumn(
                name: "EhPadraoRecebimentoFaturaCartao",
                table: "contas_gerenciais");
        }
    }
}
