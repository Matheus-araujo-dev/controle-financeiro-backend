using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PostMvpImportacaoAprendizadoRecorrencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DescricaoAjustada",
                table: "itens_importados_whatsapp",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MarcarComoRecorrente",
                table: "itens_importados_whatsapp",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DescricaoAjustada",
                table: "itens_importados_whatsapp");

            migrationBuilder.DropColumn(
                name: "MarcarComoRecorrente",
                table: "itens_importados_whatsapp");
        }
    }
}
