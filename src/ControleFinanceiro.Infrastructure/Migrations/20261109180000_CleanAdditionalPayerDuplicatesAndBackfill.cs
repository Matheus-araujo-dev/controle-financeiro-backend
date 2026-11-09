using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CleanAdditionalPayerDuplicatesAndBackfill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The previous migration (FixDuplicateImportAndUniqueIndex) only cleaned
            // duplicate contas_pagar where ChaveSerieImportacaoCartao IS NOT NULL.
            // Additional payers in rateio splits have NULL ChaveSerieImportacaoCartao,
            // so their duplicates were left behind. This migration fixes that.

            // Step 1: Delete rateios of orphaned additional-payer records.
            // These are records whose primary payer (with ChaveSerieImportacaoCartao set)
            // was already deleted by the previous migration, leaving the additional
            // payers stranded with no group leader.
            migrationBuilder.Sql("""
                DELETE FROM rateios_conta_gerencial
                WHERE "ContaPagarId" IN (
                    SELECT cp."Id"
                    FROM contas_pagar cp
                    WHERE cp."CartaoId" IS NOT NULL
                      AND cp."ChaveSerieImportacaoCartao" IS NULL
                      AND cp."GrupoPagadoresId" IS NOT NULL
                      AND NOT EXISTS (
                          SELECT 1 FROM contas_pagar pp
                          WHERE pp."GrupoPagadoresId" = cp."GrupoPagadoresId"
                            AND pp."ChaveSerieImportacaoCartao" IS NOT NULL
                      )
                );
                """);

            // Step 2: Delete contas_receber (reembolsos) linked to orphaned additional payers.
            migrationBuilder.Sql("""
                DELETE FROM contas_receber
                WHERE "GrupoReembolsoId" IN (
                    SELECT cp."GrupoReembolsoId"
                    FROM contas_pagar cp
                    WHERE cp."CartaoId" IS NOT NULL
                      AND cp."ChaveSerieImportacaoCartao" IS NULL
                      AND cp."GrupoPagadoresId" IS NOT NULL
                      AND cp."GrupoReembolsoId" IS NOT NULL
                      AND NOT EXISTS (
                          SELECT 1 FROM contas_pagar pp
                          WHERE pp."GrupoPagadoresId" = cp."GrupoPagadoresId"
                            AND pp."ChaveSerieImportacaoCartao" IS NOT NULL
                      )
                );
                """);

            // Step 3: Delete the orphaned additional-payer records themselves.
            migrationBuilder.Sql("""
                DELETE FROM contas_pagar
                WHERE "Id" IN (
                    SELECT cp."Id"
                    FROM contas_pagar cp
                    WHERE cp."CartaoId" IS NOT NULL
                      AND cp."ChaveSerieImportacaoCartao" IS NULL
                      AND cp."GrupoPagadoresId" IS NOT NULL
                      AND NOT EXISTS (
                          SELECT 1 FROM contas_pagar pp
                          WHERE pp."GrupoPagadoresId" = cp."GrupoPagadoresId"
                            AND pp."ChaveSerieImportacaoCartao" IS NOT NULL
                      )
                );
                """);

            // Step 4: For any remaining duplicates among records with NULL
            // ChaveSerieImportacaoCartao (safety net), deduplicate using broader criteria.
            migrationBuilder.Sql("""
                DELETE FROM rateios_conta_gerencial
                WHERE "ContaPagarId" IN (
                    SELECT d."Id" FROM (
                        SELECT "Id",
                               ROW_NUMBER() OVER (
                                   PARTITION BY "CartaoId", "DataVencimento", "Descricao",
                                                COALESCE("DataCompra", "DataEmissao"),
                                                "ValorLiquido", "NumeroParcela",
                                                "QuantidadeParcelas", "PagadorId"
                                   ORDER BY "CreatedAtUtc" ASC, "Id" ASC
                               ) AS rn
                        FROM contas_pagar
                        WHERE "CartaoId" IS NOT NULL
                          AND "ChaveSerieImportacaoCartao" IS NULL
                    ) d WHERE d.rn > 1
                );
                """);

            migrationBuilder.Sql("""
                DELETE FROM contas_receber
                WHERE "GrupoReembolsoId" IN (
                    SELECT dup."GrupoReembolsoId" FROM (
                        SELECT "Id", "GrupoReembolsoId",
                               ROW_NUMBER() OVER (
                                   PARTITION BY "CartaoId", "DataVencimento", "Descricao",
                                                COALESCE("DataCompra", "DataEmissao"),
                                                "ValorLiquido", "NumeroParcela",
                                                "QuantidadeParcelas", "PagadorId"
                                   ORDER BY "CreatedAtUtc" ASC, "Id" ASC
                               ) AS rn
                        FROM contas_pagar
                        WHERE "CartaoId" IS NOT NULL
                          AND "ChaveSerieImportacaoCartao" IS NULL
                    ) dup WHERE dup.rn > 1 AND dup."GrupoReembolsoId" IS NOT NULL
                );
                """);

            migrationBuilder.Sql("""
                DELETE FROM contas_pagar
                WHERE "Id" IN (
                    SELECT d."Id" FROM (
                        SELECT "Id",
                               ROW_NUMBER() OVER (
                                   PARTITION BY "CartaoId", "DataVencimento", "Descricao",
                                                COALESCE("DataCompra", "DataEmissao"),
                                                "ValorLiquido", "NumeroParcela",
                                                "QuantidadeParcelas", "PagadorId"
                                   ORDER BY "CreatedAtUtc" ASC, "Id" ASC
                               ) AS rn
                        FROM contas_pagar
                        WHERE "CartaoId" IS NOT NULL
                          AND "ChaveSerieImportacaoCartao" IS NULL
                    ) d WHERE d.rn > 1
                );
                """);

            // Step 5: Backfill ChaveSerieImportacaoCartao for surviving additional-payer
            // records, deriving the key from the matching primary payer in the same group.
            migrationBuilder.Sql("""
                UPDATE contas_pagar additional
                SET "ChaveSerieImportacaoCartao" =
                    primary_payer."ChaveSerieImportacaoCartao" || '|pagador:' || additional."PagadorId"
                FROM contas_pagar primary_payer
                WHERE additional."GrupoPagadoresId" IS NOT NULL
                  AND additional."ChaveSerieImportacaoCartao" IS NULL
                  AND additional."CartaoId" IS NOT NULL
                  AND primary_payer."GrupoPagadoresId" = additional."GrupoPagadoresId"
                  AND primary_payer."ChaveSerieImportacaoCartao" IS NOT NULL
                  AND primary_payer."NumeroParcela" = additional."NumeroParcela";
                """);

            // Step 6: Recalculate fatura totals for all affected faturas.
            migrationBuilder.Sql("""
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data cleanup is not reversible.
        }
    }
}
