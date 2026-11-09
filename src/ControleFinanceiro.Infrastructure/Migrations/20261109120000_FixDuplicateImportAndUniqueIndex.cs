using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixDuplicateImportAndUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Remove duplicate contas_pagar keeping only the oldest per unique key.
            //    Also cleans up related rateios_conta_gerencial, contas_receber
            //    (reembolsos) and itens_conciliacao links to the duplicates.
            migrationBuilder.Sql("""
                -- Identify duplicates: rows sharing (CartaoId, ChaveSerieImportacaoCartao, NumeroParcela, QuantidadeParcelas)
                -- where ChaveSerieImportacaoCartao IS NOT NULL and CartaoId IS NOT NULL.
                -- Keep the row with the smallest Id (the first created).
                WITH ranked AS (
                    SELECT "Id",
                           ROW_NUMBER() OVER (
                               PARTITION BY "CartaoId", "ChaveSerieImportacaoCartao", "NumeroParcela", "QuantidadeParcelas"
                               ORDER BY "CreatedAtUtc" ASC, "Id" ASC
                           ) AS rn
                    FROM contas_pagar
                    WHERE "CartaoId" IS NOT NULL
                      AND "ChaveSerieImportacaoCartao" IS NOT NULL
                ),
                duplicates AS (
                    SELECT "Id" FROM ranked WHERE rn > 1
                )
                -- Point any itens_conciliacao that reference a duplicate to the surviving row
                UPDATE itens_conciliacao ic
                SET "ContaPagarVinculadaId" = survivor."Id"
                FROM duplicates d
                JOIN contas_pagar dup ON dup."Id" = d."Id"
                JOIN contas_pagar survivor ON survivor."CartaoId" = dup."CartaoId"
                    AND survivor."ChaveSerieImportacaoCartao" = dup."ChaveSerieImportacaoCartao"
                    AND survivor."NumeroParcela" = dup."NumeroParcela"
                    AND survivor."QuantidadeParcelas" = dup."QuantidadeParcelas"
                    AND survivor."Id" != dup."Id"
                    AND survivor."Id" NOT IN (SELECT "Id" FROM duplicates)
                WHERE ic."ContaPagarVinculadaId" = d."Id";
                """);

            migrationBuilder.Sql("""
                -- Delete rateios of duplicates
                DELETE FROM rateios_conta_gerencial
                WHERE "ContaPagarId" IN (
                    SELECT d."Id" FROM (
                        SELECT "Id",
                               ROW_NUMBER() OVER (
                                   PARTITION BY "CartaoId", "ChaveSerieImportacaoCartao", "NumeroParcela", "QuantidadeParcelas"
                                   ORDER BY "CreatedAtUtc" ASC, "Id" ASC
                               ) AS rn
                        FROM contas_pagar
                        WHERE "CartaoId" IS NOT NULL AND "ChaveSerieImportacaoCartao" IS NOT NULL
                    ) d WHERE d.rn > 1
                );
                """);

            migrationBuilder.Sql("""
                -- Delete contas_receber (reembolsos) that reference duplicates via GrupoReembolsoId
                DELETE FROM contas_receber
                WHERE "GrupoReembolsoId" IN (
                    SELECT dup."GrupoReembolsoId" FROM (
                        SELECT "Id", "GrupoReembolsoId",
                               ROW_NUMBER() OVER (
                                   PARTITION BY "CartaoId", "ChaveSerieImportacaoCartao", "NumeroParcela", "QuantidadeParcelas"
                                   ORDER BY "CreatedAtUtc" ASC, "Id" ASC
                               ) AS rn
                        FROM contas_pagar
                        WHERE "CartaoId" IS NOT NULL AND "ChaveSerieImportacaoCartao" IS NOT NULL
                    ) dup WHERE dup.rn > 1 AND dup."GrupoReembolsoId" IS NOT NULL
                );
                """);

            migrationBuilder.Sql("""
                -- Delete the duplicate contas_pagar themselves
                DELETE FROM contas_pagar
                WHERE "Id" IN (
                    SELECT d."Id" FROM (
                        SELECT "Id",
                               ROW_NUMBER() OVER (
                                   PARTITION BY "CartaoId", "ChaveSerieImportacaoCartao", "NumeroParcela", "QuantidadeParcelas"
                                   ORDER BY "CreatedAtUtc" ASC, "Id" ASC
                               ) AS rn
                        FROM contas_pagar
                        WHERE "CartaoId" IS NOT NULL AND "ChaveSerieImportacaoCartao" IS NOT NULL
                    ) d WHERE d.rn > 1
                );
                """);

            migrationBuilder.Sql("""
                -- Recalculate fatura totals for affected faturas
                UPDATE faturas_cartao f
                SET "ValorTotal" = sub.total
                FROM (
                    SELECT "FaturaCartaoId", SUM("ValorLiquido") AS total
                    FROM contas_pagar
                    WHERE "FaturaCartaoId" IS NOT NULL AND "StatusContaId" != 5
                    GROUP BY "FaturaCartaoId"
                ) sub
                WHERE f."Id" = sub."FaturaCartaoId";
                """);

            // 2. Drop existing non-unique index and create unique one
            migrationBuilder.DropIndex(
                name: "IX_contas_pagar_CartaoId_ChaveSerieImportacaoCartao_NumeroParcela_QuantidadeParcelas",
                table: "contas_pagar");

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_CartaoId_ChaveSerieImportacaoCartao_NumeroParcela_QuantidadeParcelas",
                table: "contas_pagar",
                columns: new[] { "CartaoId", "ChaveSerieImportacaoCartao", "NumeroParcela", "QuantidadeParcelas" },
                unique: true,
                filter: "\"CartaoId\" IS NOT NULL AND \"ChaveSerieImportacaoCartao\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_contas_pagar_CartaoId_ChaveSerieImportacaoCartao_NumeroParcela_QuantidadeParcelas",
                table: "contas_pagar");

            migrationBuilder.CreateIndex(
                name: "IX_contas_pagar_CartaoId_ChaveSerieImportacaoCartao_NumeroParcela_QuantidadeParcelas",
                table: "contas_pagar",
                columns: new[] { "CartaoId", "ChaveSerieImportacaoCartao", "NumeroParcela", "QuantidadeParcelas" },
                filter: "\"CartaoId\" IS NOT NULL AND \"ChaveSerieImportacaoCartao\" IS NOT NULL");
        }
    }
}
