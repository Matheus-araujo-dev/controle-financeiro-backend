using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ApplyMemoriaEstabelecimentoConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_MemoriasEstabelecimento",
                table: "MemoriasEstabelecimento");

            migrationBuilder.RenameTable(
                name: "MemoriasEstabelecimento",
                newName: "memorias_estabelecimento");

            migrationBuilder.RenameIndex(
                name: "IX_MemoriasEstabelecimento_FamiliaId",
                table: "memorias_estabelecimento",
                newName: "IX_memorias_estabelecimento_FamiliaId");

            migrationBuilder.AlterColumn<string>(
                name: "Chave",
                table: "memorias_estabelecimento",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddPrimaryKey(
                name: "PK_memorias_estabelecimento",
                table: "memorias_estabelecimento",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_memorias_estabelecimento_CartaoId",
                table: "memorias_estabelecimento",
                column: "CartaoId");

            migrationBuilder.CreateIndex(
                name: "IX_memorias_estabelecimento_FamiliaId_CartaoId_Chave",
                table: "memorias_estabelecimento",
                columns: new[] { "FamiliaId", "CartaoId", "Chave" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_memorias_estabelecimento_cartoes_CartaoId",
                table: "memorias_estabelecimento",
                column: "CartaoId",
                principalTable: "cartoes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_memorias_estabelecimento_cartoes_CartaoId",
                table: "memorias_estabelecimento");

            migrationBuilder.DropPrimaryKey(
                name: "PK_memorias_estabelecimento",
                table: "memorias_estabelecimento");

            migrationBuilder.DropIndex(
                name: "IX_memorias_estabelecimento_CartaoId",
                table: "memorias_estabelecimento");

            migrationBuilder.DropIndex(
                name: "IX_memorias_estabelecimento_FamiliaId_CartaoId_Chave",
                table: "memorias_estabelecimento");

            migrationBuilder.RenameTable(
                name: "memorias_estabelecimento",
                newName: "MemoriasEstabelecimento");

            migrationBuilder.RenameIndex(
                name: "IX_memorias_estabelecimento_FamiliaId",
                table: "MemoriasEstabelecimento",
                newName: "IX_MemoriasEstabelecimento_FamiliaId");

            migrationBuilder.AlterColumn<string>(
                name: "Chave",
                table: "MemoriasEstabelecimento",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AddPrimaryKey(
                name: "PK_MemoriasEstabelecimento",
                table: "MemoriasEstabelecimento",
                column: "Id");
        }
    }
}
