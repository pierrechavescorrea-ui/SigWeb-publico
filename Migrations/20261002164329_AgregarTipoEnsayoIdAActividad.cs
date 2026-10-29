using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class AgregarTipoEnsayoIdAActividad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TipoEnsayoId",
                table: "Actividades",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Actividades_TipoEnsayoId",
                table: "Actividades",
                column: "TipoEnsayoId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Actividades_TiposEnsayo_TipoEnsayoId",
                table: "Actividades",
                column: "TipoEnsayoId",
                principalTable: "TiposEnsayo",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Actividades_TiposEnsayo_TipoEnsayoId",
                table: "Actividades");

            migrationBuilder.DropIndex(
                name: "IX_Actividades_TipoEnsayoId",
                table: "Actividades");

            migrationBuilder.DropColumn(
                name: "TipoEnsayoId",
                table: "Actividades");
        }
    }
}
