using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurrenceBusinessDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DiaGeracaoMensal",
                table: "regras_recorrencia",
                newName: "DiaOrdemMensal");

            migrationBuilder.AddColumn<string>(
                name: "TipoDia",
                table: "regras_recorrencia",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TipoDia",
                table: "regras_recorrencia");

            migrationBuilder.RenameColumn(
                name: "DiaOrdemMensal",
                table: "regras_recorrencia",
                newName: "DiaGeracaoMensal");
        }
    }
}
