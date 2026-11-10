using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UltraAggressiveDedup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Previous migrations deduped using Descricao and COALESCE(DataCompra,DataEmissao)
            // in the partition key. Duplicates with slight description variations (trailing
            // spaces, parcela suffixes, casing) survived. This migration uses a minimal key:
            // (CartaoId, FaturaCartaoId, ValorLiquido, NumeroParcela, QuantidadeParcelas,
            //  DataVencimento, PagadorId) — ignoring Descricao entirely.

            // Step 1a: Delete itens_conciliacao that would conflict on unique constraint.
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "Id",
                           FIRST_VALUE("Id") OVER (
                               PARTITION BY "CartaoId", "FaturaCartaoId", "ValorLiquido",
                                            "NumeroParcela", "QuantidadeParcelas",
                                            "DataVencimento", "PagadorId"
                               ORDER BY
                                   CASE WHEN "ChaveSerieImportacaoCartao" IS NOT NULL THEN 0 ELSE 1 END,
                                   "CreatedAtUtc" ASC, "Id" ASC
                           ) AS survivor_id,
                           ROW_NUMBER() OVER (
                               PARTITION BY "CartaoId", "FaturaCartaoId", "ValorLiquido",
                                            "NumeroParcela", "QuantidadeParcelas",
                                            "DataVencimento", "PagadorId"
                               ORDER BY
                                   CASE WHEN "ChaveSerieImportacaoCartao" IS NOT NULL THEN 0 ELSE 1 END,
                                   "CreatedAtUtc" ASC, "Id" ASC
                           ) AS rn
                    FROM contas_pagar
                    WHERE "CartaoId" IS NOT NULL
                      AND "FaturaCartaoId" IS NOT NULL
                      AND "StatusContaId" != '44444444-4444-4444-4444-444444444444'
                )
                DELETE FROM itens_conciliacao ic
                USING ranked r
                WHERE ic."ContaPagarVinculadaId" = r."Id"
                  AND r.rn > 1
                  AND EXISTS (
                      SELECT 1 FROM itens_conciliacao ic2
                      WHERE ic2."ConciliacaoId" = ic."ConciliacaoId"
                        AND ic2."ContaPagarVinculadaId" = r.survivor_id
                  );
                """);

            // Step 1b: Re-point remaining itens_conciliacao from duplicates to survivors.
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "Id",
                           FIRST_VALUE("Id") OVER (
                               PARTITION BY "CartaoId", "FaturaCartaoId", "ValorLiquido",
                                            "NumeroParcela", "QuantidadeParcelas",
                                            "DataVencimento", "PagadorId"
                               ORDER BY
                                   CASE WHEN "ChaveSerieImportacaoCartao" IS NOT NULL THEN 0 ELSE 1 END,
                                   "CreatedAtUtc" ASC, "Id" ASC
                           ) AS survivor_id,
                           ROW_NUMBER() OVER (
                               PARTITION BY "CartaoId", "FaturaCartaoId", "ValorLiquido",
                                            "NumeroParcela", "QuantidadeParcelas",
                                            "DataVencimento", "PagadorId"
                               ORDER BY
                                   CASE WHEN "ChaveSerieImportacaoCartao" IS NOT NULL THEN 0 ELSE 1 END,
                                   "CreatedAtUtc" ASC, "Id" ASC
                           ) AS rn
                    FROM contas_pagar
                    WHERE "CartaoId" IS NOT NULL
                      AND "FaturaCartaoId" IS NOT NULL
                      AND "StatusContaId" != '44444444-4444-4444-4444-444444444444'
                )
                UPDATE itens_conciliacao
                SET "ContaPagarVinculadaId" = r.survivor_id
                FROM ranked r
                WHERE itens_conciliacao."ContaPagarVinculadaId" = r."Id"
                  AND r.rn > 1;
                """);

            // Step 2: Delete rateios of duplicates.
            migrationBuilder.Sql("""
                DELETE FROM rateios_conta_gerencial
                WHERE "ContaPagarId" IN (
                    SELECT "Id" FROM (
                        SELECT "Id",
                               ROW_NUMBER() OVER (
                                   PARTITION BY "CartaoId", "FaturaCartaoId", "ValorLiquido",
                                                "NumeroParcela", "QuantidadeParcelas",
                                                "DataVencimento", "PagadorId"
                                   ORDER BY
                                       CASE WHEN "ChaveSerieImportacaoCartao" IS NOT NULL THEN 0 ELSE 1 END,
                                       "CreatedAtUtc" ASC, "Id" ASC
                               ) AS rn
                        FROM contas_pagar
                        WHERE "CartaoId" IS NOT NULL
                          AND "FaturaCartaoId" IS NOT NULL
                          AND "StatusContaId" != '44444444-4444-4444-4444-444444444444'
                    ) d WHERE d.rn > 1
                );
                """);

            // Step 3: Delete contas_receber (reembolsos) linked to duplicates.
            migrationBuilder.Sql("""
                DELETE FROM contas_receber
                WHERE "GrupoReembolsoId" IN (
                    SELECT cp."GrupoReembolsoId" FROM contas_pagar cp
                    INNER JOIN (
                        SELECT "Id" FROM (
                            SELECT "Id",
                                   ROW_NUMBER() OVER (
                                       PARTITION BY "CartaoId", "FaturaCartaoId", "ValorLiquido",
                                                    "NumeroParcela", "QuantidadeParcelas",
                                                    "DataVencimento", "PagadorId"
                                       ORDER BY
                                           CASE WHEN "ChaveSerieImportacaoCartao" IS NOT NULL THEN 0 ELSE 1 END,
                                           "CreatedAtUtc" ASC, "Id" ASC
                                   ) AS rn
                            FROM contas_pagar
                            WHERE "CartaoId" IS NOT NULL
                              AND "FaturaCartaoId" IS NOT NULL
                              AND "StatusContaId" != '44444444-4444-4444-4444-444444444444'
                        ) d WHERE d.rn > 1
                    ) dup ON dup."Id" = cp."Id"
                    WHERE cp."GrupoReembolsoId" IS NOT NULL
                );
                """);

            // Step 4: Delete the duplicate contas_pagar.
            migrationBuilder.Sql("""
                DELETE FROM contas_pagar
                WHERE "Id" IN (
                    SELECT "Id" FROM (
                        SELECT "Id",
                               ROW_NUMBER() OVER (
                                   PARTITION BY "CartaoId", "FaturaCartaoId", "ValorLiquido",
                                                "NumeroParcela", "QuantidadeParcelas",
                                                "DataVencimento", "PagadorId"
                                   ORDER BY
                                       CASE WHEN "ChaveSerieImportacaoCartao" IS NOT NULL THEN 0 ELSE 1 END,
                                       "CreatedAtUtc" ASC, "Id" ASC
                               ) AS rn
                        FROM contas_pagar
                        WHERE "CartaoId" IS NOT NULL
                          AND "FaturaCartaoId" IS NOT NULL
                          AND "StatusContaId" != '44444444-4444-4444-4444-444444444444'
                    ) d WHERE d.rn > 1
                );
                """);

            // Step 5: Recalculate ALL fatura totals.
            migrationBuilder.Sql("""
                UPDATE faturas_cartao f
                SET "ValorTotal" = COALESCE(sub.total, 0)
                FROM (
                    SELECT "FaturaCartaoId", SUM("ValorLiquido") AS total
                    FROM contas_pagar
                    WHERE "FaturaCartaoId" IS NOT NULL
                      AND "StatusContaId" != '44444444-4444-4444-4444-444444444444'
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
