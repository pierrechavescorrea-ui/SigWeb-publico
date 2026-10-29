using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class ActividadIcono : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TipoFijadoPorUsuario",
                table: "CamposTabla",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "IconoCatalogoId",
                table: "Actividades",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Actividades_IconoCatalogoId",
                table: "Actividades",
                column: "IconoCatalogoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Actividades_IconosCatalogo_IconoCatalogoId",
                table: "Actividades",
                column: "IconoCatalogoId",
                principalTable: "IconosCatalogo",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Actividades_IconosCatalogo_IconoCatalogoId",
                table: "Actividades");

            migrationBuilder.DropIndex(
                name: "IX_Actividades_IconoCatalogoId",
                table: "Actividades");

            migrationBuilder.DropColumn(
                name: "TipoFijadoPorUsuario",
                table: "CamposTabla");

            migrationBuilder.DropColumn(
                name: "IconoCatalogoId",
                table: "Actividades");
        }
    }
}
