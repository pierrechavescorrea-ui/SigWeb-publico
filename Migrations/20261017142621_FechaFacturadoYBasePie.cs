using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class FechaFacturadoYBasePie : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaFacturado",
                table: "RegistrosParametroTiro",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaFacturado",
                table: "RegistrosParametro",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BaseCalculo",
                table: "MedicionesIndicador",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FechaFacturado",
                table: "RegistrosParametroTiro");

            migrationBuilder.DropColumn(
                name: "FechaFacturado",
                table: "RegistrosParametro");

            migrationBuilder.DropColumn(
                name: "BaseCalculo",
                table: "MedicionesIndicador");
        }
    }
}
