using Microsoft.EntityFrameworkCore.Migrations;

namespace ControleFinanceiro.Infrastructure.Migrations;

public partial class RepairLegacyImportedItemWorkspace : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Somente o pai conhecido comprova propriedade; nunca escolher uma familia arbitraria.
        migrationBuilder.Sql("""
            UPDATE "itens_importados_whatsapp"
            SET "FamiliaId" = (
                SELECT p."FamiliaId"
                FROM "importacoes_whatsapp" p
                WHERE p."Id" = "itens_importados_whatsapp"."ImportacaoWhatsappId"
            )
            WHERE "FamiliaId" = '00000000-0000-0000-0000-000000000000'
              AND EXISTS (
                SELECT 1
                FROM "importacoes_whatsapp" p
                JOIN "familias" f ON f."Id" = p."FamiliaId"
                WHERE p."Id" = "itens_importados_whatsapp"."ImportacaoWhatsappId"
                  AND p."FamiliaId" <> '00000000-0000-0000-0000-000000000000'
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // A propriedade recuperada nao deve ser removida em rollback.
    }
}
