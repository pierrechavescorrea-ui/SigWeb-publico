using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class AgregarVisibilidadColumnasFijasATipoEnsayo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MostrarDisenoMezcla",
                table: "TiposEnsayo",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MostrarElemento",
                table: "TiposEnsayo",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MostrarFechaEjecucion",
                table: "TiposEnsayo",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MostrarInforme",
                table: "TiposEnsayo",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MostrarDisenoMezcla",
                table: "TiposEnsayo");

            migrationBuilder.DropColumn(
                name: "MostrarElemento",
                table: "TiposEnsayo");

            migrationBuilder.DropColumn(
                name: "MostrarFechaEjecucion",
                table: "TiposEnsayo");

            migrationBuilder.DropColumn(
                name: "MostrarInforme",
                table: "TiposEnsayo");
        }
    }
}
