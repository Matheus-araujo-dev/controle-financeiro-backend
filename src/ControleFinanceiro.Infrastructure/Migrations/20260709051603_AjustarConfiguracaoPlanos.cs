using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFinanceiro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AjustarConfiguracaoPlanos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_movimentacoes_financeiras_Transferencias_TransferenciaId",
                table: "movimentacoes_financeiras");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Transferencias",
                table: "Transferencias");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Planos",
                table: "Planos");

            migrationBuilder.RenameTable(
                name: "Transferencias",
                newName: "transferencias");

            migrationBuilder.RenameTable(
                name: "Planos",
                newName: "planos");

            migrationBuilder.RenameIndex(
                name: "IX_Transferencias_FamiliaId",
                table: "transferencias",
                newName: "IX_transferencias_FamiliaId");

            migrationBuilder.RenameIndex(
                name: "IX_Planos_FamiliaId",
                table: "planos",
                newName: "IX_planos_FamiliaId");

            migrationBuilder.AlterColumn<decimal>(
                name: "Valor",
                table: "transferencias",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "Descricao",
                table: "transferencias",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ValorMensal",
                table: "planos",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalRetirado",
                table: "planos",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "Nome",
                table: "planos",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Descricao",
                table: "planos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_transferencias",
                table: "transferencias",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_planos",
                table: "planos",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_transferencias_ContaBancariaDestinoId",
                table: "transferencias",
                column: "ContaBancariaDestinoId");

            migrationBuilder.CreateIndex(
                name: "IX_transferencias_ContaBancariaOrigemId",
                table: "transferencias",
                column: "ContaBancariaOrigemId");

            migrationBuilder.CreateIndex(
                name: "IX_transferencias_DataTransferencia",
                table: "transferencias",
                column: "DataTransferencia");

            migrationBuilder.CreateIndex(
                name: "IX_planos_ContaBancariaCaixaId",
                table: "planos",
                column: "ContaBancariaCaixaId");

            migrationBuilder.AddForeignKey(
                name: "FK_movimentacoes_financeiras_transferencias_TransferenciaId",
                table: "movimentacoes_financeiras",
                column: "TransferenciaId",
                principalTable: "transferencias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_planos_contas_bancarias_ContaBancariaCaixaId",
                table: "planos",
                column: "ContaBancariaCaixaId",
                principalTable: "contas_bancarias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_transferencias_contas_bancarias_ContaBancariaDestinoId",
                table: "transferencias",
                column: "ContaBancariaDestinoId",
                principalTable: "contas_bancarias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_transferencias_contas_bancarias_ContaBancariaOrigemId",
                table: "transferencias",
                column: "ContaBancariaOrigemId",
                principalTable: "contas_bancarias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_movimentacoes_financeiras_transferencias_TransferenciaId",
                table: "movimentacoes_financeiras");

            migrationBuilder.DropForeignKey(
                name: "FK_planos_contas_bancarias_ContaBancariaCaixaId",
                table: "planos");

            migrationBuilder.DropForeignKey(
                name: "FK_transferencias_contas_bancarias_ContaBancariaDestinoId",
                table: "transferencias");

            migrationBuilder.DropForeignKey(
                name: "FK_transferencias_contas_bancarias_ContaBancariaOrigemId",
                table: "transferencias");

            migrationBuilder.DropPrimaryKey(
                name: "PK_transferencias",
                table: "transferencias");

            migrationBuilder.DropIndex(
                name: "IX_transferencias_ContaBancariaDestinoId",
                table: "transferencias");

            migrationBuilder.DropIndex(
                name: "IX_transferencias_ContaBancariaOrigemId",
                table: "transferencias");

            migrationBuilder.DropIndex(
                name: "IX_transferencias_DataTransferencia",
                table: "transferencias");

            migrationBuilder.DropPrimaryKey(
                name: "PK_planos",
                table: "planos");

            migrationBuilder.DropIndex(
                name: "IX_planos_ContaBancariaCaixaId",
                table: "planos");

            migrationBuilder.RenameTable(
                name: "transferencias",
                newName: "Transferencias");

            migrationBuilder.RenameTable(
                name: "planos",
                newName: "Planos");

            migrationBuilder.RenameIndex(
                name: "IX_transferencias_FamiliaId",
                table: "Transferencias",
                newName: "IX_Transferencias_FamiliaId");

            migrationBuilder.RenameIndex(
                name: "IX_planos_FamiliaId",
                table: "Planos",
                newName: "IX_Planos_FamiliaId");

            migrationBuilder.AlterColumn<decimal>(
                name: "Valor",
                table: "Transferencias",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "Descricao",
                table: "Transferencias",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ValorMensal",
                table: "Planos",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalRetirado",
                table: "Planos",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "Nome",
                table: "Planos",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Descricao",
                table: "Planos",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Transferencias",
                table: "Transferencias",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Planos",
                table: "Planos",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_movimentacoes_financeiras_Transferencias_TransferenciaId",
                table: "movimentacoes_financeiras",
                column: "TransferenciaId",
                principalTable: "Transferencias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
