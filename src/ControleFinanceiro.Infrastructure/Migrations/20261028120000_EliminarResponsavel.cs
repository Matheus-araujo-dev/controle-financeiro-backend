using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EliminarResponsavel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. contas_pagar ──────────────────────────────────────────────
            migrationBuilder.RenameColumn(
                name: "ResponsavelCompraId",
                table: "contas_pagar",
                newName: "PagadorId");

            migrationBuilder.RenameIndex(
                name: "IX_contas_pagar_ResponsavelCompraId",
                table: "contas_pagar",
                newName: "IX_contas_pagar_PagadorId");

            migrationBuilder.RenameColumn(
                name: "GrupoResponsaveisId",
                table: "contas_pagar",
                newName: "GrupoPagadoresId");

            migrationBuilder.RenameIndex(
                name: "IX_contas_pagar_GrupoResponsaveisId",
                table: "contas_pagar",
                newName: "IX_contas_pagar_GrupoPagadoresId");

            // ── 2. contas_receber ────────────────────────────────────────────
            migrationBuilder.RenameColumn(
                name: "ResponsavelId",
                table: "contas_receber",
                newName: "RecebedorId");

            migrationBuilder.RenameIndex(
                name: "IX_contas_receber_ResponsavelId",
                table: "contas_receber",
                newName: "IX_contas_receber_RecebedorId");

            migrationBuilder.RenameColumn(
                name: "GrupoResponsaveisId",
                table: "contas_receber",
                newName: "GrupoRecebedoresId");

            migrationBuilder.RenameIndex(
                name: "IX_contas_receber_GrupoResponsaveisId",
                table: "contas_receber",
                newName: "IX_contas_receber_GrupoRecebedoresId");

            // ── 3. contas_gerenciais ─────────────────────────────────────────
            migrationBuilder.RenameColumn(
                name: "ResponsavelPadraoId",
                table: "contas_gerenciais",
                newName: "MembroPadraoId");

            migrationBuilder.RenameIndex(
                name: "IX_contas_gerenciais_ResponsavelPadraoId",
                table: "contas_gerenciais",
                newName: "IX_contas_gerenciais_MembroPadraoId");

            // ── 4. compras_planejadas ────────────────────────────────────────
            migrationBuilder.RenameColumn(
                name: "ResponsavelId",
                table: "compras_planejadas",
                newName: "PagadorId");

            migrationBuilder.RenameIndex(
                name: "IX_compras_planejadas_ResponsavelId",
                table: "compras_planejadas",
                newName: "IX_compras_planejadas_PagadorId");

            // ── 5. itens_importados_whatsapp ─────────────────────────────────
            migrationBuilder.RenameColumn(
                name: "ResponsavelId",
                table: "itens_importados_whatsapp",
                newName: "PagadorId");

            migrationBuilder.RenameIndex(
                name: "IX_itens_importados_whatsapp_ResponsavelId",
                table: "itens_importados_whatsapp",
                newName: "IX_itens_importados_whatsapp_PagadorId");

            // ── 6. pessoas: migrar dados e remover EhResponsavel ─────────────
            migrationBuilder.Sql("""
                UPDATE "pessoas"
                SET "EhPagador" = true, "EhRecebedor" = true
                WHERE "EhResponsavel" = true
                  AND ("EhPagador" = false OR "EhRecebedor" = false);
                """);

            migrationBuilder.DropColumn(
                name: "EhResponsavel",
                table: "pessoas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ── pessoas: restaurar EhResponsavel ─────────────────────────────
            migrationBuilder.AddColumn<bool>(
                name: "EhResponsavel",
                table: "pessoas",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql("""
                UPDATE "pessoas"
                SET "EhResponsavel" = true
                WHERE "EhPagador" = true AND "EhRecebedor" = true;
                """);

            // ── itens_importados_whatsapp ─────────────────────────────────────
            migrationBuilder.RenameColumn(
                name: "PagadorId",
                table: "itens_importados_whatsapp",
                newName: "ResponsavelId");

            migrationBuilder.RenameIndex(
                name: "IX_itens_importados_whatsapp_PagadorId",
                table: "itens_importados_whatsapp",
                newName: "IX_itens_importados_whatsapp_ResponsavelId");

            // ── compras_planejadas ────────────────────────────────────────────
            migrationBuilder.RenameColumn(
                name: "PagadorId",
                table: "compras_planejadas",
                newName: "ResponsavelId");

            migrationBuilder.RenameIndex(
                name: "IX_compras_planejadas_PagadorId",
                table: "compras_planejadas",
                newName: "IX_compras_planejadas_ResponsavelId");

            // ── contas_gerenciais ─────────────────────────────────────────────
            migrationBuilder.RenameColumn(
                name: "MembroPadraoId",
                table: "contas_gerenciais",
                newName: "ResponsavelPadraoId");

            migrationBuilder.RenameIndex(
                name: "IX_contas_gerenciais_MembroPadraoId",
                table: "contas_gerenciais",
                newName: "IX_contas_gerenciais_ResponsavelPadraoId");

            // ── contas_receber ────────────────────────────────────────────────
            migrationBuilder.RenameColumn(
                name: "RecebedorId",
                table: "contas_receber",
                newName: "ResponsavelId");

            migrationBuilder.RenameIndex(
                name: "IX_contas_receber_RecebedorId",
                table: "contas_receber",
                newName: "IX_contas_receber_ResponsavelId");

            migrationBuilder.RenameColumn(
                name: "GrupoRecebedoresId",
                table: "contas_receber",
                newName: "GrupoResponsaveisId");

            migrationBuilder.RenameIndex(
                name: "IX_contas_receber_GrupoRecebedoresId",
                table: "contas_receber",
                newName: "IX_contas_receber_GrupoResponsaveisId");

            // ── contas_pagar ──────────────────────────────────────────────────
            migrationBuilder.RenameColumn(
                name: "PagadorId",
                table: "contas_pagar",
                newName: "ResponsavelCompraId");

            migrationBuilder.RenameIndex(
                name: "IX_contas_pagar_PagadorId",
                table: "contas_pagar",
                newName: "IX_contas_pagar_ResponsavelCompraId");

            migrationBuilder.RenameColumn(
                name: "GrupoPagadoresId",
                table: "contas_pagar",
                newName: "GrupoResponsaveisId");

            migrationBuilder.RenameIndex(
                name: "IX_contas_pagar_GrupoPagadoresId",
                table: "contas_pagar",
                newName: "IX_contas_pagar_GrupoResponsaveisId");
        }
    }
}
