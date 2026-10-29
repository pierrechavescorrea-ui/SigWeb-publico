using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class AgregarIconosColoresPorElemento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ColorMapa",
                table: "ElementosTrazado",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IconoMapa",
                table: "ElementosTrazado",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ColorMapa",
                table: "Componentes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IconoMapa",
                table: "Componentes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MostrarEnMapa",
                table: "Componentes",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ColorMapa",
                table: "ElementosTrazado");

            migrationBuilder.DropColumn(
                name: "IconoMapa",
                table: "ElementosTrazado");

            migrationBuilder.DropColumn(
                name: "ColorMapa",
                table: "Componentes");

            migrationBuilder.DropColumn(
                name: "IconoMapa",
                table: "Componentes");

            migrationBuilder.DropColumn(
                name: "MostrarEnMapa",
                table: "Componentes");
        }
    }
}
