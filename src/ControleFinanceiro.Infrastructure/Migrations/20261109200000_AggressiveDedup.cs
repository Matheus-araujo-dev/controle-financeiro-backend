using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AggressiveDedup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Previous migrations may not have cleaned all duplicates.
            // This migration does a comprehensive, business-level dedup of ALL
            // card-imported contas_pagar, regardless of ChaveSerieImportacaoCartao.
            //
            // A duplicate is defined as two or more non-cancelled records sharing:
            // (CartaoId, DataVencimento, Descricao, COALESCE(DataCompra, DataEmissao),
            //  ValorLiquido, NumeroParcela, QuantidadeParcelas, PagadorId)
            //
            // The survivor is picked by: FaturaCartaoId set > ChaveSerieImportacaoCartao set > earliest.

            // Step 1: Re-point itens_conciliacao from duplicates to their survivors.
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "Id",
                           FIRST_VALUE("Id") OVER (
                               PARTITION BY "CartaoId", "DataVencimento", "Descricao",
                                            COALESCE("DataCompra", "DataEmissao"),
                                            "ValorLiquido", "NumeroParcela",
                                            "QuantidadeParcelas", "PagadorId"
                               ORDER BY
                                   CASE WHEN "FaturaCartaoId" IS NOT NULL THEN 0 ELSE 1 END,
                                   CASE WHEN "ChaveSerieImportacaoCartao" IS NOT NULL THEN 0 ELSE 1 END,
                                   "CreatedAtUtc" ASC, "Id" ASC
                           ) AS survivor_id,
                           ROW_NUMBER() OVER (
                               PARTITION BY "CartaoId", "DataVencimento", "Descricao",
                                            COALESCE("DataCompra", "DataEmissao"),
                                            "ValorLiquido", "NumeroParcela",
                                            "QuantidadeParcelas", "PagadorId"
                               ORDER BY
                                   CASE WHEN "FaturaCartaoId" IS NOT NULL THEN 0 ELSE 1 END,
                                   CASE WHEN "ChaveSerieImportacaoCartao" IS NOT NULL THEN 0 ELSE 1 END,
                                   "CreatedAtUtc" ASC, "Id" ASC
                           ) AS rn
                    FROM contas_pagar
                    WHERE "CartaoId" IS NOT NULL
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
                                   PARTITION BY "CartaoId", "DataVencimento", "Descricao",
                                                COALESCE("DataCompra", "DataEmissao"),
                                                "ValorLiquido", "NumeroParcela",
                                                "QuantidadeParcelas", "PagadorId"
                                   ORDER BY
                                       CASE WHEN "FaturaCartaoId" IS NOT NULL THEN 0 ELSE 1 END,
                                       CASE WHEN "ChaveSerieImportacaoCartao" IS NOT NULL THEN 0 ELSE 1 END,
                                       "CreatedAtUtc" ASC, "Id" ASC
                               ) AS rn
                        FROM contas_pagar
                        WHERE "CartaoId" IS NOT NULL
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
                                       PARTITION BY "CartaoId", "DataVencimento", "Descricao",
                                                    COALESCE("DataCompra", "DataEmissao"),
                                                    "ValorLiquido", "NumeroParcela",
                                                    "QuantidadeParcelas", "PagadorId"
                                       ORDER BY
                                           CASE WHEN "FaturaCartaoId" IS NOT NULL THEN 0 ELSE 1 END,
                                           CASE WHEN "ChaveSerieImportacaoCartao" IS NOT NULL THEN 0 ELSE 1 END,
                                           "CreatedAtUtc" ASC, "Id" ASC
                                   ) AS rn
                            FROM contas_pagar
                            WHERE "CartaoId" IS NOT NULL
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
                                   PARTITION BY "CartaoId", "DataVencimento", "Descricao",
                                                COALESCE("DataCompra", "DataEmissao"),
                                                "ValorLiquido", "NumeroParcela",
                                                "QuantidadeParcelas", "PagadorId"
                                   ORDER BY
                                       CASE WHEN "FaturaCartaoId" IS NOT NULL THEN 0 ELSE 1 END,
                                       CASE WHEN "ChaveSerieImportacaoCartao" IS NOT NULL THEN 0 ELSE 1 END,
                                       "CreatedAtUtc" ASC, "Id" ASC
                               ) AS rn
                        FROM contas_pagar
                        WHERE "CartaoId" IS NOT NULL
                          AND "StatusContaId" != '44444444-4444-4444-4444-444444444444'
                    ) d WHERE d.rn > 1
                );
                """);

            // Step 5: Backfill ChaveSerieImportacaoCartao for any remaining records
            // that have it NULL but belong to a group with a primary that has it set.
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

            // Step 6: Recalculate ALL fatura totals from surviving records.
            migrationBuilder.Sql("""
                UPDATE faturas_cartao f
                SET "ValorTotal" = COALESCE(sub.total, 0)
                FROM (
                    SELECT "FaturaCartaoId", SUM("ValorLiquido") AS total
                    FROM contas_pagar
                    WHERE "FaturaCartaoId" IS NOT NULL AND "StatusContaId" != '44444444-4444-4444-4444-444444444444'
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
