using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class AgregarGeometriaComponente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GeometriaJson",
                table: "Componentes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InformacionAdicional",
                table: "Componentes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MiniaturaDwg",
                table: "Componentes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrigenArchivoDwg",
                table: "Componentes",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GeometriaJson",
                table: "Componentes");

            migrationBuilder.DropColumn(
                name: "InformacionAdicional",
                table: "Componentes");

            migrationBuilder.DropColumn(
                name: "MiniaturaDwg",
                table: "Componentes");

            migrationBuilder.DropColumn(
                name: "OrigenArchivoDwg",
                table: "Componentes");
        }
    }
}
