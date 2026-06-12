using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarMultiTenantFamilia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FamiliaId",
                table: "regras_recorrencia",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "FamiliaId",
                table: "rateios_conta_gerencial",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "FamiliaId",
                table: "pessoas",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "FamiliaId",
                table: "movimentacoes_financeiras",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "FamiliaId",
                table: "itens_importados_whatsapp",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "FamiliaId",
                table: "importacoes_whatsapp",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "FamiliaId",
                table: "formas_pagamento",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "FamiliaId",
                table: "faturas_cartao",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "FamiliaId",
                table: "contas_receber",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "FamiliaId",
                table: "contas_pagar",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "FamiliaId",
                table: "contas_gerenciais",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "FamiliaId",
                table: "contas_bancarias",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "FamiliaId",
                table: "compras_planejadas",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "FamiliaId",
                table: "cartoes",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_regras_recorrencia_FamiliaId",
                table: "regras_recorrencia",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_rateios_conta_gerencial_FamiliaId",
                table: "rateios_conta_gerencial",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_pessoas_FamiliaId",
                table: "pessoas",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_movimentacoes_financeiras_FamiliaId",
                table: "movimentacoes_financeiras",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_itens_importados_whatsapp_FamiliaId",
                table: "itens_importados_whatsapp",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_importacoes_whatsapp_FamiliaId",
                table: "importacoes_whatsapp",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_formas_pagamento_FamiliaId",
                table: "formas_pagamento",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_faturas_cartao_FamiliaId",
                table: "faturas_cartao",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_receber_FamiliaId",
                table: "contas_receber",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_FamiliaId",
                table: "contas_pagar",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_gerenciais_FamiliaId",
                table: "contas_gerenciais",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_contas_bancarias_FamiliaId",
                table: "contas_bancarias",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_compras_planejadas_FamiliaId",
                table: "compras_planejadas",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_cartoes_FamiliaId",
                table: "cartoes",
                column: "FamiliaId");

            // Backfill: cria a Família Padrão e associa todo o histórico pré-multi-tenant a ela.
            // O primeiro usuário a logar com Google assume esta família (Identidade:FamiliaPadraoId).
            migrationBuilder.Sql($"""
                IF NOT EXISTS (SELECT 1 FROM familias WHERE Id = '{FamiliaPadraoId}')
                BEGIN
                    INSERT INTO familias (Id, Nome, Ativa, CreatedAtUtc, UpdatedAtUtc, CreatedBy, UpdatedBy)
                    VALUES ('{FamiliaPadraoId}', N'Família Padrão', 1, SYSUTCDATETIME(), SYSUTCDATETIME(), 'system', 'system');
                END
                """);

            string[] tabelasTenant =
            [
                "regras_recorrencia", "rateios_conta_gerencial", "pessoas", "movimentacoes_financeiras",
                "itens_importados_whatsapp", "importacoes_whatsapp", "formas_pagamento", "faturas_cartao",
                "contas_receber", "contas_pagar", "contas_gerenciais", "contas_bancarias",
                "compras_planejadas", "cartoes"
            ];

            foreach (var tabela in tabelasTenant)
            {
                migrationBuilder.Sql(
                    $"UPDATE {tabela} SET FamiliaId = '{FamiliaPadraoId}' WHERE FamiliaId = '00000000-0000-0000-0000-000000000000';");
            }
        }

        private const string FamiliaPadraoId = "00000000-0000-0000-0000-000000000001";

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_regras_recorrencia_FamiliaId",
                table: "regras_recorrencia");

            migrationBuilder.DropIndex(
                name: "IX_rateios_conta_gerencial_FamiliaId",
                table: "rateios_conta_gerencial");

            migrationBuilder.DropIndex(
                name: "IX_pessoas_FamiliaId",
                table: "pessoas");

            migrationBuilder.DropIndex(
                name: "IX_movimentacoes_financeiras_FamiliaId",
                table: "movimentacoes_financeiras");

            migrationBuilder.DropIndex(
                name: "IX_itens_importados_whatsapp_FamiliaId",
                table: "itens_importados_whatsapp");

            migrationBuilder.DropIndex(
                name: "IX_importacoes_whatsapp_FamiliaId",
                table: "importacoes_whatsapp");

            migrationBuilder.DropIndex(
                name: "IX_formas_pagamento_FamiliaId",
                table: "formas_pagamento");

            migrationBuilder.DropIndex(
                name: "IX_faturas_cartao_FamiliaId",
                table: "faturas_cartao");

            migrationBuilder.DropIndex(
                name: "IX_contas_receber_FamiliaId",
                table: "contas_receber");

            migrationBuilder.DropIndex(
                name: "IX_contas_pagar_FamiliaId",
                table: "contas_pagar");

            migrationBuilder.DropIndex(
                name: "IX_contas_gerenciais_FamiliaId",
                table: "contas_gerenciais");

            migrationBuilder.DropIndex(
                name: "IX_contas_bancarias_FamiliaId",
                table: "contas_bancarias");

            migrationBuilder.DropIndex(
                name: "IX_compras_planejadas_FamiliaId",
                table: "compras_planejadas");

            migrationBuilder.DropIndex(
                name: "IX_cartoes_FamiliaId",
                table: "cartoes");

            migrationBuilder.DropColumn(
                name: "FamiliaId",
                table: "regras_recorrencia");

            migrationBuilder.DropColumn(
                name: "FamiliaId",
                table: "rateios_conta_gerencial");

            migrationBuilder.DropColumn(
                name: "FamiliaId",
                table: "pessoas");

            migrationBuilder.DropColumn(
                name: "FamiliaId",
                table: "movimentacoes_financeiras");

            migrationBuilder.DropColumn(
                name: "FamiliaId",
                table: "itens_importados_whatsapp");

            migrationBuilder.DropColumn(
                name: "FamiliaId",
                table: "importacoes_whatsapp");

            migrationBuilder.DropColumn(
                name: "FamiliaId",
                table: "formas_pagamento");

            migrationBuilder.DropColumn(
                name: "FamiliaId",
                table: "faturas_cartao");

            migrationBuilder.DropColumn(
                name: "FamiliaId",
                table: "contas_receber");

            migrationBuilder.DropColumn(
                name: "FamiliaId",
                table: "contas_pagar");

            migrationBuilder.DropColumn(
                name: "FamiliaId",
                table: "contas_gerenciais");

            migrationBuilder.DropColumn(
                name: "FamiliaId",
                table: "contas_bancarias");

            migrationBuilder.DropColumn(
                name: "FamiliaId",
                table: "compras_planejadas");

            migrationBuilder.DropColumn(
                name: "FamiliaId",
                table: "cartoes");
        }
    }
}
