using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class SincronizarCatalogoLocalizacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ElementoTrazadoId",
                table: "Catalogos",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Catalogos_ElementoTrazadoId",
                table: "Catalogos",
                column: "ElementoTrazadoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Catalogos_ElementosTrazado_ElementoTrazadoId",
                table: "Catalogos",
                column: "ElementoTrazadoId",
                principalTable: "ElementosTrazado",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Catalogos_ElementosTrazado_ElementoTrazadoId",
                table: "Catalogos");

            migrationBuilder.DropIndex(
                name: "IX_Catalogos_ElementoTrazadoId",
                table: "Catalogos");

            migrationBuilder.DropColumn(
                name: "ElementoTrazadoId",
                table: "Catalogos");
        }
    }
}
